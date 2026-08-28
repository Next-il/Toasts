# workshop

The panorama files this project owns.

```
panorama/layout/custom_game/    layouts
panorama/styles/custom_game/    stylesheets, including a copy of hud_shared.css
```

**Each project keeps its own.** A repo that gets split out takes its layouts with it and still
builds, rather than depending on a folder somewhere else in a monorepo that will not come with it.

`hud_shared.css` is deliberately duplicated for the same reason - it is the card, header, footer and
reveal that every layout includes, so a project without it is broken. The copies must stay identical:
`collect-panorama.py` compares them and refuses to assemble mismatched ones, because the alternative
is whichever project was copied last silently winning.

## Building

From the repo root, assemble every project's files into your addon and compile:

```
python3 Shared/tools/collect-panorama.py --out "X:/.../content/csgo_addons/hud_test1"
Shared/tools/build-hud.cmd
```

Or in one step:

```
Shared/tools/build-hud.cmd -Collect
```

Validate before compiling, from anywhere inside the project:

```
python3 Shared/tools/validate.py .
```
