#!/usr/bin/env python3
"""QMS Weaver 앱 아이콘 생성 — 오버·언더 직조(weave) 패턴, 딥 틸 배경."""
from PIL import Image, ImageDraw

S = 256
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# 배경: 라운드 사각, 딥 틸
BG = (23, 98, 122, 255)        # #17627A
d.rounded_rectangle([8, 8, S - 8, S - 8], radius=52, fill=BG)

# 스트립(직조 밴드) 좌표
W = 34                          # 밴드 폭
P1, P2 = 76, 146                # 두 밴드의 시작 위치
L, R = 40, S - 40               # 밴드 길이 범위
H = (222, 236, 243, 255)        # 가로 밴드 색 (밝음)  #DEECF3
V = (154, 197, 214, 255)       # 세로 밴드 색 (중간)  #9AC5D6
RAD = 17

def hband(y):
    d.rounded_rectangle([L, y, R, y + W], radius=RAD, fill=H)

def vband(x):
    d.rounded_rectangle([x, L, x + W, R], radius=RAD, fill=V)

def hpatch(x, y):
    # 교차점에서 가로 밴드를 다시 위에 그려 오버-언더 짜임을 만든다
    d.rectangle([x - 6, y, x + W + 6, y + W], fill=H)

# 짜임: 가로 2개 → 세로 2개(위) → 교차 2곳은 가로가 다시 위로
hband(P1); hband(P2)
vband(P1); vband(P2)
hpatch(P1, P2)   # (v1 위로 h2)
hpatch(P2, P1)   # (v2 위로 h1)

# 매듭 노드: 중앙 교차 4점에 작은 딥 틸 점 — 네트워크 노드 암시
for cx in (P1 + W // 2, P2 + W // 2):
    for cy in (P1 + W // 2, P2 + W // 2):
        d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=BG)

img.save("src/QmsWeaver.App/Assets/qmsweaver.png")
img.save("src/QmsWeaver.App/Assets/qmsweaver.ico",
         sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("icon written")
