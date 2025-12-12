"""
Basic Analysis Example
~~~~~~~~~~~~~~~~~~~~~~~

Simple example demonstrating basic usage of Medical Form Analyzer.
"""

import sys
from pathlib import Path

# Add parent directory to path
sys.path.append(str(Path(__file__).parent.parent))

from src.main import MedicalFormAnalyzer


def main():
    """Run basic analysis."""
    print("Medical Form Analyzer - Basic Example")
    print("=" * 80)

    # Initialize analyzer
    print("\n1. Initializing analyzer...")
    analyzer = MedicalFormAnalyzer()

    # Example document (you need to provide your own)
    document_path = "data/sample_forms/inspection_form_example.png"

    # Check if file exists
    if not Path(document_path).exists():
        print(f"Error: {document_path} not found.")
        print("Please place a sample document in data/sample_forms/")
        return

    # Analyze document
    print(f"\n2. Analyzing document: {document_path}")
    result = analyzer.analyze(document_path)

    # Display summary
    print("\n3. Analysis Summary")
    print("-" * 80)
    summary = result.get_summary()

    print(f"Overall Score: {summary['overall_score']:.1f}/100 (Grade: {summary['grade']})")
    print(f"Risk Level: {summary['risk_level'].upper()}")
    print(f"Elements Detected: {summary['element_count']}")
    print(f"Potential Blind Spots: {summary['blind_spots']}")
    print(f"High-Risk Zones: {summary['high_risk_zones']}")
    print(f"Recommendations: {summary['recommendation_count']}")

    # Save visualizations
    print("\n4. Saving visualizations...")
    output_dir = Path("data/test_results")
    output_dir.mkdir(parents=True, exist_ok=True)

    result.visualize(str(output_dir / "analysis_composite.png"), "composite")
    result.visualize(str(output_dir / "analysis_heatmap.png"), "heatmap")
    result.visualize(str(output_dir / "analysis_risk_zones.png"), "risk_zones")
    result.visualize(str(output_dir / "analysis_dashboard.png"), "dashboard")

    print(f"   ✓ Saved to {output_dir}/")

    # Generate report
    print("\n5. Generating report...")
    result.generate_report(str(output_dir / "analysis_report.txt"))

    print(f"   ✓ Report saved to {output_dir}/analysis_report.txt")

    # Display top recommendations
    print("\n6. Top Recommendations")
    print("-" * 80)
    for i, rec in enumerate(result.recommendations[:3], 1):
        print(f"\n{i}. [{rec['priority'].upper()}] {rec['title']}")
        print(f"   {rec['description']}")

    print("\n" + "=" * 80)
    print("Analysis complete! Check the output directory for results.")


if __name__ == "__main__":
    main()
