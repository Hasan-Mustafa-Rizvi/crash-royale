# Tools

Python scripts that made the game's original art and sound. You don't need them to build or play the game; their
output is already in the repo. They're here so the assets can be changed or regenerated.

| Script | Makes |
|---|---|
| `gen_ui.py` | UI sprites (panels, buttons, frames, icons, crowns, elixir drop), emote icons and card portraits |
| `gen_sfx.py` | Every sound effect, synthesized from scratch |

```
pip install -r Tools/requirements.txt
python Tools/gen_ui.py
python Tools/gen_sfx.py
```

Run them from the repo root, then use **Crash Royale > Rebuild Content and Scenes** in Unity.
