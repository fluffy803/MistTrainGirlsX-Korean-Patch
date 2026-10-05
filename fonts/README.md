# Font: MistX KR

`MistX KR` is a merged/renamed font (SIL OFL 1.1). The .ttf itself is shipped in
the **release zip** (not tracked in git; see `.gitignore`).

Build recipe (reproduce):
1. Base: Cafe24 Ssurround Air (OFL) — https://fonts.cafe24.com
2. Fill missing CJK bracket/punctuation glyphs from Noto Sans KR (OFL).
3. Fill Japanese kana/kanji (BMP) from Noto Sans JP (OFL).
4. Rename the family to `MistX KR` (avoid Reserved Font Names) and re-save.

License: `OFL.txt`.
