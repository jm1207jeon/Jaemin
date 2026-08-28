#!/usr/bin/env python3
"""
QMS 문서 마스터 리스트(F401-09 문서 등록 이력 대장)에서
문서 관계 네트워크 시드(seedNetwork.json)를 생성한다.

계층 규칙 (문서번호가 위상을 인코딩):
  QM-001                      품질매뉴얼 (루트)
  QP-{ccc}                    절차서, ccc의 첫 자리 = ISO 13485 조항 (4~8)
  SOP-{ccc}-{nn}              지침서/작업표준서 → QP-{ccc}의 하위
  F{ccc}-{nn}                 양식 → QP-{ccc}의 하위
  F{ccc}-{nn}-{mm}            양식 → SOP-{ccc}-{nn}의 하위

사용법: python3 tools/build_network.py <master-list.xlsx> <out.json>
"""
import sys, json, re
import openpyxl

DOC_ID = re.compile(r'^(QM|QP|SOP|F)[\d-]+')


def clean(s):
    return ' '.join(str(s).split()) if s is not None else ''


def read_sheet(wb, sheet, category):
    """Approved 시트에서 (id, 한글명, 영문명, rev, 유효일, 부서) 추출"""
    docs = []
    for row in wb[sheet].iter_rows(min_row=2):
        doc_id = clean(row[0].value)
        if not DOC_ID.match(doc_id):
            continue
        raw_name = str(row[1].value or '')
        parts = [p.strip() for p in raw_name.split('\n') if p.strip()]
        name_kr = parts[0] if parts else doc_id
        name_en = parts[1] if len(parts) > 1 else ''
        rev = clean(row[2].value)
        eff = str(row[4].value or '')[:10]
        dept = clean(row[5].value)
        docs.append(dict(id=doc_id, name=name_kr, nameEn=name_en,
                         rev=rev, effective=eff, dept=dept, category=category))
    return docs


def parent_of(doc_id):
    """번호 규칙으로 상위 문서 ID를 유도"""
    if doc_id.startswith('QP-') or doc_id == 'QM-001':
        return 'QM-001' if doc_id != 'QM-001' else None
    m = re.match(r'^SOP-(\d{3})-\d+$', doc_id)
    if m:
        return f'QP-{m.group(1)}'
    m = re.match(r'^F(\d{3})-(\d+)-(\d+)$', doc_id)
    if m:  # SOP 하위 양식 (예: F707-01-05 → SOP-707-01)
        return f'SOP-{m.group(1)}-{m.group(2)}'
    m = re.match(r'^F(\d{3})-(\d+)$', doc_id)
    if m:
        return f'QP-{m.group(1)}'
    return None


# ── 외부 노드: 규제(regulation) · 규격(standard) ──────────────────────────
EXTERNAL = [
    # 규제 (5대 필수 커버리지: ISO 13485 / KGMP / MDSAP / FDA / MDR)
    dict(id='REG-ISO13485', name='ISO 13485:2016', type='standard',
         desc='의료기기 QMS 국제규격 — 전체 문서체계의 뼈대'),
    dict(id='REG-KGMP', name='KGMP (MFDS)', type='regulation',
         desc='의료기기 제조 및 품질관리 기준 (식약처 고시)'),
    dict(id='REG-MDSAP', name='MDSAP', type='regulation',
         desc='단일심사 프로그램 — US·CA·BR·AU·JP 5개국'),
    dict(id='REG-FDA', name='FDA 21 CFR 820 (QMSR)', type='regulation',
         desc='미국 품질시스템 규정 + MDR(803)·C&R(806) 보고'),
    dict(id='REG-MDR', name='EU MDR 2017/745', type='regulation',
         desc='유럽 의료기기 규정 — 기술문서·PMS·Vigilance'),
    dict(id='REG-UKCA', name='UK MDR 2002', type='regulation', desc='영국 UKCA'),
    dict(id='REG-PMDA', name='일본 PMD Act / MO169', type='regulation', desc='일본 QMS 성령'),
    dict(id='REG-HC', name='캐나다 CMDR SOR/98-282', type='regulation', desc='캐나다 의료기기 규정'),
    dict(id='REG-ANVISA', name='브라질 RDC 665/2022', type='regulation', desc='브라질 BGMP'),
    dict(id='REG-TGA', name='호주 TG (MD) Regulations', type='regulation', desc='호주 규정'),
    # 프로세스 규격
    dict(id='STD-14971', name='ISO 14971:2019', type='standard', desc='위험관리'),
    dict(id='STD-10993', name='ISO 10993-1', type='standard', desc='생물학적 평가'),
    dict(id='STD-11135', name='ISO 11135', type='standard', desc='EO 멸균 밸리데이션'),
    dict(id='STD-11737', name='ISO 11737', type='standard', desc='미생물학적 방법'),
    dict(id='STD-11607', name='ISO 11607', type='standard', desc='멸균포장'),
    dict(id='STD-62366', name='IEC 62366-1', type='standard', desc='사용적합성'),
    dict(id='STD-14155', name='ISO 14155 / KGCP', type='standard', desc='임상시험 GCP'),
    dict(id='STD-15223', name='ISO 15223-1 · ISO 20417', type='standard', desc='라벨링 기호·정보제공'),
    dict(id='STD-14644', name='ISO 14644', type='standard', desc='클린룸 환경'),
]

# 규제/규격 → 담당 문서 매핑 (외부요구 엣지)
EXT_EDGES = {
    'REG-ISO13485': ['QM-001'],
    'REG-KGMP': ['QP-813', 'QP-820', 'QM-001'],
    'REG-FDA': ['QP-815', 'QP-821', 'QP-826'],
    'REG-MDR': ['QP-827', 'QP-809', 'QP-812', 'QP-803', 'SOP-709-01', 'QP-709'],
    'REG-UKCA': ['QP-828', 'QP-829'],
    'REG-PMDA': ['QP-819', 'QP-825'],
    'REG-HC': ['QP-817', 'QP-822'],
    'REG-ANVISA': ['QP-818', 'QP-823'],
    'REG-TGA': ['QP-816', 'QP-824'],
    # MDSAP = 5개국 절차 총괄 + 내부심사
    'REG-MDSAP': ['QP-815', 'QP-816', 'QP-817', 'QP-818', 'QP-819', 'QP-804'],
    'STD-14971': ['QP-701'],
    'STD-10993': ['QP-703'],
    'STD-11135': ['SOP-705-29', 'SOP-805-04', 'QP-706'],
    'STD-11737': ['SOP-805-04', 'SOP-706-01'],
    'STD-11607': ['SOP-805-05', 'SOP-705-36', 'QP-706'],
    'STD-62366': ['SOP-703-05'],
    'STD-14155': ['QP-803'],
    'STD-15223': ['QP-709', 'SOP-709-01'],
    'STD-14644': ['SOP-603-01', 'QP-603'],
}

# 프로세스 흐름 크로스 링크 (계층 외 연결 — 양식이 증거인 것 위주)
CROSS_EDGES = [
    # 피드백 루프: 불만 → 각국 당국 보고 (F802-08~13이 증거)
    ('QP-802', 'QP-820', '불만 → 한국 부작용 보고 평가 (F802-08)'),
    ('QP-802', 'QP-821', '불만 → 미국 MDR 보고 평가 (F802-09)'),
    ('QP-802', 'QP-809', '불만 → 유럽 Vigilance 평가 (F802-10)'),
    ('QP-802', 'QP-823', '불만 → 브라질 보고 평가 (F802-11)'),
    ('QP-802', 'QP-822', '불만 → 캐나다 보고 평가 (F802-12)'),
    ('QP-802', 'QP-824', '불만 → 호주 보고 평가 (F802-13)'),
    ('QP-802', 'QP-825', '불만 → 일본 사고 보고'),
    ('QP-802', 'QP-829', '불만 → 영국 사고 보고'),
    # CAPA 허브
    ('QP-802', 'QP-808', '불만 → 시정예방조치'),
    ('QP-806', 'QP-808', '부적합 → 시정예방조치'),
    ('QP-804', 'QP-808', '내부심사 부적합 → CAPA'),
    ('QP-805', 'QP-806', '검사 불합격 → 부적합품 처리'),
    ('QP-808', 'QP-811', '중대 결함 → 리콜'),
    ('QP-809', 'QP-811', 'Vigilance → 리콜/FSCA'),
    ('QP-826', 'QP-811', '미국 C&R 보고 ↔ 리콜'),
    # 설계·위험·밸리데이션
    ('QP-703', 'QP-701', '설계개발 ↔ 위험관리 (설계입력·검증)'),
    ('QP-703', 'QP-706', '설계이관 → 공정 밸리데이션'),
    ('QP-703', 'QP-705', '설계산출물(DMR) → 공정관리'),
    ('QP-703', 'QP-803', '설계검증 ↔ 임상평가'),
    ('QP-701', 'QP-812', '위험관리 ↔ 시판후감시 (잔여위험 재평가)'),
    # 시판후 루프
    ('QP-812', 'QP-802', 'PMS 입력 ← 고객불만'),
    ('QP-812', 'QP-801', 'PMS 입력 ← 피드백'),
    ('QP-812', 'QP-803', 'PMCF ↔ 임상평가'),
    # 구매·협력·검사
    ('QP-704', 'QP-711', '구매 ↔ 협력업체 평가'),
    ('QP-704', 'QP-805', '구매품 → 수입검사 (F704-08)'),
    # 경영·분석
    ('QP-807', 'QP-502', '데이터분석 → 경영검토 입력'),
    ('QP-804', 'QP-502', '내부심사 결과 → 경영검토'),
    ('QP-802', 'QP-807', '불만 트렌드 → 데이터분석'),
    ('QP-501', 'QP-502', '품질목표 ↔ 경영검토'),
    # 추적성·식별
    ('QP-707', 'QP-708', '식별추적 ↔ 자재제품관리'),
    ('QP-705', 'QP-706', '공정관리 ↔ 공정 밸리데이션'),
    ('QP-705', 'QP-707', '공정 ↔ 식별추적 (공정이송표)'),
    ('QP-709', 'QP-707', '라벨링 ↔ UDI'),
    # 교육·문서
    ('QP-601', 'QM-001', '교육훈련 — 전 문서 개정 시 교육'),
    ('QP-401', 'QP-402', '문서관리 ↔ 기록관리'),
]

# ── 파생(예상) 노드: 기록·기술문서 계층 + 갭 후보 ────────────────────────
DERIVED = [
    # 절차가 생성하는 핵심 기술문서/파일 (existing=근거 양식 존재)
    dict(id='TD-DMR', name='제품표준서 (DMR)', type='techdoc', gap=False,
         src='F703-11', links=['QP-703', 'QP-705', 'REG-FDA']),
    dict(id='TD-DHF', name='설계이력파일 (DHF)', type='techdoc', gap=False,
         src='F703-12/13', links=['QP-703', 'REG-FDA']),
    dict(id='TD-DHR', name='제품이력기록 (DHR)', type='techdoc', gap=False,
         src='F402-04', links=['QP-402', 'QP-705', 'QP-707']),
    dict(id='TD-RMF', name='위험관리파일 (RMF)', type='techdoc', gap=False,
         src='F701-01~05', links=['QP-701', 'STD-14971']),
    dict(id='TD-CER', name='임상평가보고서 (CER)', type='techdoc', gap=False,
         src='F803-2', links=['QP-803', 'REG-MDR']),
    dict(id='TD-TF', name='EU 기술문서 (Annex II·III)', type='techdoc', gap=False,
         src='QP-827', links=['QP-827', 'REG-MDR', 'TD-CER', 'TD-RMF']),
    dict(id='TD-510K', name='미국 510(k) 파일', type='techdoc', gap=False,
         src='QP-815', links=['QP-815', 'REG-FDA']),
    dict(id='TD-PMS', name='PMS 파일 (PSUR·PMCF)', type='techdoc', gap=False,
         src='F812-01~12', links=['QP-812', 'REG-MDR']),
    dict(id='TD-STERVAL', name='EO 멸균 밸리데이션 파일', type='techdoc', gap=False,
         src='QP-706', links=['QP-706', 'STD-11135', 'SOP-705-29']),
    # 갭 후보: 규제상 요구되나 목록에 전담 문서가 안 보이는 것
    dict(id='GAP-BIO', name='생체적합성 평가 보고서 (ISO 10993)', type='techdoc', gap=True,
         src='전담 절차/지침 미확인', links=['QP-703', 'STD-10993']),
    dict(id='GAP-SHELF', name='유효기간·안정성 파일 (Shelf-life)', type='techdoc', gap=True,
         src='전담 문서 미확인', links=['QP-703', 'QP-706']),
    dict(id='GAP-USAB', name='사용적합성 파일 (IEC 62366)', type='techdoc', gap=True,
         src='SOP-703-05 Rev.0(2021) — 파일 성숙도 확인 필요', links=['SOP-703-05', 'STD-62366']),
    dict(id='GAP-CSV', name='소프트웨어 밸리데이션 (QMS SW, 13485 §4.1.6)', type='techdoc', gap=True,
         src='전담 절차 미확인', links=['QP-401', 'QP-706', 'REG-ISO13485']),
]

CAT_TYPE = {'manual-procedure': None, 'sop': 'sop', 'work-standard': 'work-standard', 'form': 'form'}


def main(xlsx_path, out_path):
    wb = openpyxl.load_workbook(xlsx_path, data_only=True)
    docs = []
    docs += read_sheet(wb, '매뉴얼 절차서_Approved(편집X)', 'manual-procedure')
    docs += read_sheet(wb, '지침서_Approved(편집X)', 'sop')
    docs += read_sheet(wb, '작업표준서_Approved(편집X)', 'work-standard')
    docs += read_sheet(wb, '양식_Approved(편집X)', 'form')

    nodes, edges, seen = [], [], set()

    def add_node(n):
        if n['id'] not in seen:
            seen.add(n['id'])
            nodes.append(n)

    def add_edge(s, t, etype, label='', strength=0.7):
        if s in seen and t in seen and s != t:
            edges.append(dict(source=s, target=t, type=etype, label=label, strength=strength))

    for d in docs:
        ntype = ('manual' if d['id'].startswith('QM') else
                 'procedure' if d['id'].startswith('QP') else
                 CAT_TYPE[d['category']] or 'sop')
        clause = None
        m = re.match(r'^(?:QP-|SOP-|F)(\d)', d['id'])
        if m:
            clause = int(m.group(1))
        add_node(dict(id=d['id'], name=d['name'], nameEn=d['nameEn'], type=ntype,
                      rev=d['rev'], effective=d['effective'], dept=d['dept'], clause=clause))

    for x in EXTERNAL:
        add_node(dict(id=x['id'], name=x['name'], type=x['type'], desc=x['desc']))
    for dv in DERIVED:
        add_node(dict(id=dv['id'], name=dv['name'], type=dv['type'],
                      gap=dv['gap'], src=dv['src']))

    # 1) 계층 엣지 (번호 규칙)
    broken = []
    for d in docs:
        p = parent_of(d['id'])
        if p:
            if p in seen:
                add_edge(d['id'], p, 'hierarchy', strength=0.9)
            else:
                broken.append((d['id'], p))

    # 2) 외부요구 엣지
    for ext, targets in EXT_EDGES.items():
        for t in targets:
            add_edge(ext, t, 'external', strength=0.85)

    # 3) 프로세스 크로스 링크
    for s, t, lab in CROSS_EDGES:
        add_edge(s, t, 'process', lab, strength=0.65)

    # 4) 파생 노드 링크
    for dv in DERIVED:
        for t in dv['links']:
            add_edge(dv['id'], t, 'derived', strength=0.6)

    counts = {}
    for n in nodes:
        counts[n['type']] = counts.get(n['type'], 0) + 1

    out = dict(
        meta=dict(source='F401-09 문서 등록 이력 대장 Rev.1 (2025-08-26 기준 Approved)',
                  generated='tools/build_network.py',
                  nodeCounts=counts, edgeCount=len(edges),
                  brokenRefs=[dict(doc=a, missingParent=b) for a, b in broken]),
        nodes=nodes, edges=edges)

    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump(out, f, ensure_ascii=False, indent=1)

    print('nodes:', len(nodes), counts)
    print('edges:', len(edges))
    print('broken parent refs (참조 무결성 경고):')
    for a, b in broken:
        print(f'  {a} → {b} (상위 문서가 목록에 없음)')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
