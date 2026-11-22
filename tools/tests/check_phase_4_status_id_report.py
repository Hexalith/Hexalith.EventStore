"""Check the actual xUnit Release generator report against the fixed status-link case inventory."""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import phase_4_status_id_gate as S


if __name__ == "__main__":
    report = S.check_cases(S.GENERATORS, Path(sys.argv[1]).read_bytes())
    labels = report["passing_cases"]
    expected = {"published Int16 1", "published Int64 1", "published Int64 2", "published Double 1",
                "published Byte 2", "v1 length 128", "v1 length 129"}
    observed = {name.rsplit(" [", 1)[1][:-1] for name in labels if name.endswith("]")}
    if observed != expected or len(labels) != 73:
        raise SystemExit("The Release report differs from the owner's typed, boundary or remaining generator cases")
    print(f"Verified {len(labels)} generated status-link cases, including {', '.join(sorted(observed))}")
