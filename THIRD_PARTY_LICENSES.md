# Third-Party Components

This patch bundles / builds upon the following components. Each retains its own
license; see the linked upstream for full terms.

## BepInEx (5.4.23.2)
- Unity mod loader framework. License: LGPL-2.1.
- https://github.com/BepInEx/BepInEx
- Bundled: `winhttp.dll`, `doorstop_config.ini`, `BepInEx/core/*` in the release zip.

## XUnity.AutoTranslator (5.6.2) + XUnity.ResourceRedirector
- Runtime text translation framework, used to apply the UI / master-data dictionary.
- https://github.com/bbepis/XUnity.AutoTranslator
- Bundled: `BepInEx/plugins/XUnity.AutoTranslator/*`, `XUnity.ResourceRedirector/*`.

## Font: "MistX KR" (SIL Open Font License 1.1)
- A MERGED / MODIFIED font: Cafe24 Ssurround Air (base, Korean) + Noto Sans JP/KR
  (Japanese kana/kanji + CJK punctuation glyphs), renamed to avoid Reserved Font Names.
- Sources (both OFL 1.1):
  - Cafe24 Ssurround Air — Cafe24 Corp. — https://fonts.cafe24.com
  - Noto Sans KR / Noto Sans JP — The Noto Project Authors / Google
- Full text: `fonts/OFL.txt`.

## Translation text
- Korean translation is a fan translation. The original Japanese text is
  copyrighted by EXNOA LLC / the game's rights holders. Distributed here only as
  a translation overlay dictionary; no original game assets are included.
