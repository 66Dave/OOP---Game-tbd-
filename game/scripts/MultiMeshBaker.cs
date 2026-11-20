using Godot;
using System.Collections.Generic;

// Bakes EVERY repeated mesh (grass, trees, rocks, bushes...) under the Sources
// into one MultiMeshInstance3D per unique mesh+material, as children of this node.
// Attach to a plain Node3D, fill in Sources, click "Bake", then "Hide Originals".
[Tool]
public partial class MultiMeshBaker : Node3D
{
	// Parents to scan, e.g. Environment and Environment2
	[Export] public Godot.Collections.Array<Node3D> Sources { get; set; } = new();

	// Meshes used fewer times than this are left alone (baking a single object gains nothing)
	[Export] public int MinInstances { get; set; } = 2;

	[ExportToolButton("Bake")]
	public Callable BakeButton => Callable.From(Bake);

	[ExportToolButton("Hide Originals")]
	public Callable HideButton => Callable.From(() => SetOriginalsVisible(false));

	[ExportToolButton("Show Originals")]
	public Callable ShowButton => Callable.From(() => SetOriginalsVisible(true));

	private void Bake()
	{
		Node sceneRoot = GetTree().EditedSceneRoot;
		if (sceneRoot == null) { GD.PushWarning("MultiMeshBaker: run this in the editor."); return; }

		// Remove results of a previous bake
		foreach (Node child in GetChildren())
		{
			if (child is MultiMeshInstance3D)
			{
				RemoveChild(child);
				child.QueueFree();
			}
		}

		Dictionary<(Mesh, Material), List<MeshInstance3D>> groups = BuildGroups(out int skipped);

		Transform3D toLocal = GlobalTransform.AffineInverse();
		int bakedGroups = 0, bakedObjects = 0;

		foreach (var pair in groups)
		{
			(Mesh mesh, Material material) = pair.Key;
			List<MeshInstance3D> list = pair.Value;

			// Format -> mesh -> count -> transforms (changing count later wipes transforms)
			var mm = new MultiMesh();
			mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
			mm.Mesh = mesh;
			mm.InstanceCount = list.Count;
			for (int i = 0; i < list.Count; i++)
				mm.SetInstanceTransform(i, toLocal * list[i].GlobalTransform);

			var mmi = new MultiMeshInstance3D();
			mmi.Name = "MM_" + (string.IsNullOrEmpty(mesh.ResourceName) ? bakedGroups.ToString() : mesh.ResourceName);
			mmi.Multimesh = mm;
			mmi.MaterialOverride = material;      // null = use the mesh's own materials
			mmi.CastShadow = list[0].CastShadow;

			AddChild(mmi, true);
			mmi.Owner = sceneRoot;                // without this the node isn't saved in the .tscn

			bakedGroups++;
			bakedObjects += list.Count;
		}

		GD.Print($"MultiMeshBaker: {bakedObjects} objects -> {bakedGroups} MultiMeshes. " +
				 $"Skipped {skipped} (multi-material overrides). Now click 'Hide Originals'.");
	}

	private void SetOriginalsVisible(bool visible)
	{
		int count = 0;
		foreach (var list in BuildGroups(out _).Values)
			foreach (MeshInstance3D mi in list) { mi.Visible = visible; count++; }

		GD.Print($"MultiMeshBaker: {(visible ? "showed" : "hid")} {count} original meshes.");
	}

	// Groups every MeshInstance3D under Sources by (mesh, material)
	private Dictionary<(Mesh, Material), List<MeshInstance3D>> BuildGroups(out int skipped)
	{
		var all = new List<MeshInstance3D>();
		int nullSources = 0;
		foreach (Node3D source in Sources)
		{
			if (source == null) { nullSources++; continue; }
			Collect(source, all);
		}
		GD.Print($"MultiMeshBaker: {Sources.Count} source slot(s), {nullSources} empty, " +
				 $"found {all.Count} MeshInstance3D node(s).");

		var groups = new Dictionary<(Mesh, Material), List<MeshInstance3D>>();
		skipped = 0;

		foreach (MeshInstance3D mi in all)
		{
			if (!TryGetMaterial(mi, out Material material)) { skipped++; continue; }

			var key = (mi.Mesh, material);
			if (!groups.TryGetValue(key, out var list))
			{
				list = new List<MeshInstance3D>();
				groups[key] = list;
			}
			list.Add(mi);
		}

		// Drop meshes that only appear a few times
		var result = new Dictionary<(Mesh, Material), List<MeshInstance3D>>();
		foreach (var pair in groups)
			if (pair.Value.Count >= MinInstances) result[pair.Key] = pair.Value;

		GD.Print($"MultiMeshBaker: {groups.Count} unique mesh/material combo(s), " +
				 $"{result.Count} used at least {MinInstances} times.");
		return result;
	}

	// Works out which single material to put on the MultiMeshInstance3D.
	// Returns false if the node has different override materials per surface (can't be baked as one).
	private static bool TryGetMaterial(MeshInstance3D mi, out Material material)
	{
		material = mi.MaterialOverride;
		if (material != null) return true;

		int surfaces = mi.Mesh.GetSurfaceCount();
		bool anyOverride = false;
		for (int s = 0; s < surfaces; s++)
			if (mi.GetSurfaceOverrideMaterial(s) != null) anyOverride = true;

		if (!anyOverride) return true;                      // mesh's own materials, nothing to copy
		if (surfaces == 1) { material = mi.GetSurfaceOverrideMaterial(0); return true; }
		return false;                                       // per-surface overrides: leave it alone
	}

	// Recursively walks everything under 'node', including nested scene instances
	private void Collect(Node node, List<MeshInstance3D> list)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is MeshInstance3D mi && mi.Mesh != null)
				list.Add(mi);

			Collect(child, list);
		}
	}
}
