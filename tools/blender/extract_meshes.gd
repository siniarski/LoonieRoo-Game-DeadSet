extends SceneTree
## Turns the imported enemy .glb files into mesh and material resources the enemy scenes use.
##
## Run after the Blender scripts and a Godot import:
##   godot --headless --import --path .
##   godot --headless --path . --script res://tools/blender/extract_meshes.gd
##
## For every assets/models/enemies/<Enemy>.json manifest it writes
##   assets/models/enemies/<Enemy>/M_*.tres   one StandardMaterial3D per flat colour
##   assets/models/enemies/<Enemy>/<Part>.res one ArrayMesh per rigid part, using those materials
## Materials are built from the manifest (not from the glTF), so colours, emission and the
## unshaded flag are exactly what StyleApplier expects.

const DIR := "res://assets/models/enemies/"

func _init() -> void:
	var only := OS.get_cmdline_user_args()
	for file in DirAccess.get_files_at(DIR):
		if not file.ends_with(".json"):
			continue
		var enemy := file.get_basename()
		if only.size() > 0 and not only.has(enemy):
			continue
		_extract(enemy)
	quit()

func _extract(enemy: String) -> void:
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(DIR + enemy + ".json"))
	var out := DIR + enemy + "/"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(out))

	var materials := {}
	for name in manifest.materials:
		var spec: Dictionary = manifest.materials[name]
		var mat := StandardMaterial3D.new()
		mat.resource_name = name
		mat.albedo_color = Color(spec.color[0], spec.color[1], spec.color[2])
		mat.roughness = 0.85
		if spec.unshaded:
			mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		if spec.emission != null:
			mat.emission_enabled = true
			mat.emission = Color(spec.emission[0], spec.emission[1], spec.emission[2])
			mat.emission_energy_multiplier = spec.energy
		var path: String = out + name + ".tres"
		ResourceSaver.save(mat, path)
		mat.take_over_path(path)
		materials[name] = mat

	var scene: Node = (load(DIR + enemy + ".glb") as PackedScene).instantiate()
	var total := 0
	for part in manifest.parts:
		var node := scene.find_child(part.name, true, false) as MeshInstance3D
		if node == null:
			push_error("%s: part %s missing from the glb" % [enemy, part.name])
			continue
		var mesh: ArrayMesh = node.mesh.duplicate(true)
		mesh.resource_name = part.name
		var verts := 0
		for i in mesh.get_surface_count():
			var source := mesh.surface_get_material(i)
			var key: String = source.resource_name if source != null else ""
			if not materials.has(key):
				push_error("%s/%s: surface %d has unknown material '%s'" % [enemy, part.name, i, key])
				continue
			mesh.surface_set_material(i, materials[key])
			verts += mesh.surface_get_array_len(i)
		if mesh.shadow_mesh != null:
			for i in mesh.shadow_mesh.get_surface_count():
				mesh.shadow_mesh.surface_set_material(i, null)
		ResourceSaver.save(mesh, out + part.name + ".res", ResourceSaver.FLAG_COMPRESS)
		total += verts
		print("  %s/%s: %d verts, %d surfaces" % [enemy, part.name, verts, mesh.get_surface_count()])
	print("%s: %d verts in %d parts" % [enemy, total, manifest.parts.size()])
	scene.free()
