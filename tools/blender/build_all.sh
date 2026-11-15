#!/bin/sh
# Regenerates enemy models end to end: Blender -> .glb -> Godot import -> mesh resources -> scenes.
#   sh tools/blender/build_all.sh [Enemy ...]      (no names = every script in tools/blender)
cd "$(dirname "$0")/../.." || exit 1
BLENDER="${BLENDER:-/c/Program Files/Blender Foundation/Blender 5.2/blender.exe}"
GODOT="${GODOT:-/c/Program Files/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe}"
NAMES="$*"
if [ -z "$NAMES" ]; then
	NAMES=$(ls assets/models/enemies/*.json 2>/dev/null | xargs -n1 basename | sed 's/\.json$//')
fi
for name in $NAMES; do
	script="tools/blender/$(echo "$name" | tr 'A-Z' 'a-z').py"
	"$BLENDER" --background --python "$script" 2>&1 | grep -E "^(PART|MODEL)|Error|Traceback|^  File" 
done
"$GODOT" --headless --import --path . 2>&1 | grep -iE "^ERROR|SCRIPT ERROR" | head
"$GODOT" --headless --path . --script res://tools/blender/extract_meshes.gd -- $NAMES 2>&1 | grep -E "verts|ERROR" 
python tools/blender/build_scenes.py $NAMES
