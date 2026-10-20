using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	private sealed class Part
	{
		public ModelFile.Mesh Mesh;
		public ModelEntity Entity;
		public Model Model;
		public Vertex[] Vertices;
		public Matrix4x4 World;
		public bool Animated;
	}

	private readonly ModelFile modelFile;
	private readonly ModelAnimation animation;
	private readonly Part[] parts;

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];
		modelFile = new ModelFile( $"lobby/terrain/{modelPrefix}_isle.md2" );

		// "M" is the slot the game loops while the model is running
		animation = new ModelAnimation( $"lobby/terrain/{modelPrefix}_isleM1.md2" );

		parts = new Part[modelFile.Meshes.Count];

		for ( int meshIdx = 0; meshIdx < modelFile.Meshes.Count; meshIdx++ )
		{
			var mesh = modelFile.Meshes[meshIdx];
			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();

			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i )
				{
					textures.Add( Texture.Missing );
				}
				else
				{
					var j = mesh.Materials[i];
					textures.Add( j.Name == null ? Texture.Missing : new Texture( $"lobby/terrain/textures/{j.Name}.wct", TextureFlags.Repeat ) );
				}
			}

			material.Set( $"Color", [.. textures] );

			var vertices = new Vertex[mesh.Vertices.Length];
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
			{
				vertices[i] = new Vertex()
				{
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags
				};
			}

			var part = new Part { Mesh = mesh, Vertices = vertices, World = mesh.WorldTransform };
			Bake( part, null );

			part.Model = new Model( vertices, mesh.Indices, material );
			part.Entity = new ModelEntity()
			{
				Model = part.Model,
				Rotation = Quaternion.Identity,
				Position = Position + new Vector3( 0, 0, -2.5f ),
			};

			parts[meshIdx] = part;
		}
	}

	/// <summary>
	/// Applies the world transform to the vertices rather than decomposing it: some matrices
	/// aren't pure rotations (e.g. l_trunk2 in Jun_isle.md2), and the game uses them as they are.
	/// </summary>
	private static void Bake( Part part, Vector3[] positions )
	{
		var mesh = part.Mesh;
		for ( int i = 0; i < part.Vertices.Length; ++i )
		{
			var source = positions != null ? positions[i] : mesh.Vertices[i].Position;
			var position = System.Numerics.Vector3.Transform( source.GetSystemVector3(), part.World );
			var normal = System.Numerics.Vector3.TransformNormal( mesh.Normals[i].GetSystemVector3(), part.World );

			part.Vertices[i].Position = new Vector3( position.X, position.Z, position.Y );
			part.Vertices[i].Normal = new Vector3( normal.X, normal.Z, normal.Y ).Normal;
		}
	}

	protected override void OnUpdate()
	{
		if ( animation.Length <= 0 )
			return;

		float frame = animation.StartFrame + (Time.Now * ModelAnimation.FramesPerSecond) % animation.Length;
		var pose = animation.Evaluate( modelFile, frame );

		for ( int i = 0; i < parts.Length; i++ )
		{
			var part = parts[i];
			part.Entity.Model = pose.Hidden[i] ? null : part.Model;

			var positions = pose.Positions[i];
			var texCoords = pose.TexCoords[i];
			bool moved = pose.WorldTransforms[i] != part.World;

			if ( !moved && positions == null && texCoords == null && !part.Animated )
				continue;

			part.World = pose.WorldTransforms[i];
			Bake( part, positions );

			texCoords ??= part.Mesh.TexCoords;
			for ( int j = 0; j < part.Vertices.Length && j < texCoords.Length; j++ )
				part.Vertices[j].TexCoords = texCoords[j];

			// Keep updating once animated, so the rest pose comes back when a track ends
			part.Animated = moved || positions != null || texCoords != null;
			part.Model.UpdateVertices( part.Vertices );
		}
	}
}
