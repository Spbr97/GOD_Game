"""Assign stable localization keys to authored Act I assets and seed English text.

Run from the repository root. Existing table entries are never overwritten, so
English copy can be edited independently after the initial import.
"""

from pathlib import Path
import re


ROOT = Path(__file__).resolve().parent.parent
TABLE = ROOT / "Assets/Resources/Localization/Strings_en.asset"
KINDS = {
    "Quests": ("questId", "quest", "title"),
    "Memories": ("memoryId", "memory", "title"),
    "Items": ("itemId", "item", "displayName"),
    "Skills": ("skillId", "skill", "displayName"),
}


def field(text: str, name: str) -> str:
    """Read a simple Unity YAML scalar, including its wrapped continuation."""
    match = re.search(rf"^  {re.escape(name)}: (.*)((?:\n    [^\n]*)*)", text, re.M)
    if not match:
        raise ValueError(f"Missing {name}")
    value = " ".join(part.strip() for part in [match.group(1), *match.group(2).splitlines()]).strip()
    if value.startswith("'") and value.endswith("'"):
        return value[1:-1].replace("''", "'")
    return value


def yaml_string(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def add_entry(rows: dict[str, tuple[str, str]], key: str, value: str, note: str) -> None:
    if key in rows:
        raise ValueError(f"Duplicate content key {key}")
    rows[key] = (value, note)


def main() -> None:
    rows: dict[str, tuple[str, str]] = {}
    for folder, (id_field, prefix, name_field) in KINDS.items():
        for path in sorted((ROOT / "Assets/Data" / folder).glob("*.asset")):
            source = path.read_text(encoding="utf-8")
            asset_id = field(source, id_field)
            key = f"{prefix}.{asset_id.lower()}"
            existing = re.search(r"^  localizationKey: ?(.*)$", source, re.M)
            if existing:
                if existing.group(1).strip() == "":
                    source = source[:existing.start()] + f"  localizationKey: {key}" + source[existing.end():]
                    path.write_text(source, encoding="utf-8")
                elif existing.group(1).strip() != key:
                    raise ValueError(f"Unexpected localization key in {path}")
            else:
                path.write_text(source.rstrip() + f"\n  localizationKey: {key}\n", encoding="utf-8")

            kind = folder[:-3] + "y" if folder == "Memories" else folder[:-1]
            add_entry(rows, f"{key}.{'title' if prefix in ('quest', 'memory') else 'name'}",
                      field(source, name_field), f"{kind} display name ({asset_id}).")
            description = field(source, "description")
            if description:
                add_entry(rows, f"{key}.description", description,
                          f"{kind} description ({asset_id}).")

            if prefix == "quest":
                rewards = field(source, "rewardsSummary")
                if rewards:
                    add_entry(rows, f"{key}.rewards", rewards,
                              f"Quest reward summary ({asset_id}).")
                for objective_id, description in re.findall(
                    r"^  - ObjectiveId: ([^\n]+)\n    Description: ([^\n]*)", source, re.M
                ):
                    add_entry(rows, f"{key}.objective.{objective_id.lower()}", description,
                              f"Quest objective ({asset_id}/{objective_id}).")

    table = TABLE.read_text(encoding="utf-8")
    # Earlier imports could have written empty descriptions for intentionally
    # unnamed item/skill blurbs. Keep the source asset's empty fallback instead.
    table = re.sub(
        r"  - Key: (?:item|skill)\.[^\n]+\.description\n    Value: ''\n    Note: [^\n]+\n",
        "", table)
    for key in sorted(rows):
        if re.search(rf"^  - Key: {re.escape(key)}$", table, re.M):
            continue
        value, note = rows[key]
        table += (f"  - Key: {key}\n"
                  f"    Value: {yaml_string(value)}\n"
                  f"    Note: {yaml_string(note)}\n")
    TABLE.write_text(table, encoding="utf-8")
    print(f"Keyed {sum(1 for _ in rows)} content fields across {sum(1 for folder in KINDS for _ in (ROOT / 'Assets/Data' / folder).glob('*.asset'))} assets")


if __name__ == "__main__":
    main()
