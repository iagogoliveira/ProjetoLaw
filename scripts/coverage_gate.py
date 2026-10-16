"""Portão de cobertura da definição de pronto (seção 1.4): >= 80% de linhas em Domain e Application.

Une todos os relatórios Cobertura gerados pelo coverlet (um por projeto de teste): uma linha conta como
coberta se algum projeto de teste a executou. Uso:

    python scripts/coverage_gate.py TestResults [--minimum 80]
"""

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

# Camadas sujeitas ao portão: Domain/Application de cada módulo e os equivalentes nos BuildingBlocks.
GATED = re.compile(r"^LexFlow\.(.+\.(Domain|Application)|SharedKernel|Application\.Abstractions)$")


def normalize(filename: str) -> str | None:
    """Mesmo arquivo pode vir com caminho absoluto ou mapeado (/_/src/...): usa a partir da pasta LexFlow.*.
    Código gerado pelo compilador (*.g.cs) fica fora do portão."""
    parts = filename.replace("\\", "/").split("/")
    if parts[-1].endswith(".g.cs"):
        return None
    for i, part in enumerate(parts):
        if part.startswith("LexFlow."):
            return "/".join(parts[i:])
    return "/".join(parts)


def collect(results: Path) -> dict[str, dict[tuple[str, int], bool]]:
    lines: dict[str, dict[tuple[str, int], bool]] = defaultdict(dict)
    reports = sorted(results.rglob("*cobertura*.xml"))
    if not reports:
        sys.exit(f"Nenhum relatório Cobertura em {results}.")
    for report in reports:
        for package in ET.parse(report).getroot().iter("package"):
            name = package.get("name", "")
            if not GATED.match(name):
                continue
            for cls in package.iter("class"):
                filename = normalize(cls.get("filename", ""))
                if filename is None:
                    continue
                for line in cls.iter("line"):
                    key = (filename, int(line.get("number", "0")))
                    hit = int(line.get("hits", "0")) > 0
                    lines[name][key] = lines[name].get(key, False) or hit
    return lines


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("results", type=Path)
    parser.add_argument("--minimum", type=float, default=80.0)
    args = parser.parse_args()

    failed = False
    print(f"{'Pacote':45} {'Linhas':>8} {'Cobertura':>10}")
    for name, lines in sorted(collect(args.results).items()):
        covered = sum(lines.values())
        rate = 100.0 * covered / len(lines) if lines else 100.0
        status = "ok" if rate >= args.minimum else "ABAIXO"
        failed |= rate < args.minimum
        print(f"{name:45} {len(lines):8d} {rate:9.1f}% {status}")

    if failed:
        print(f"\nCobertura abaixo de {args.minimum:.0f}% em Domain/Application.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
