using System.Numerics;

namespace OpenTPW;

/// <summary>A lobby model with shared MD2 vertex, UV and node animation playback.</summary>
public sealed class LobbyModel : Entity
{
	private static readonly Dictionary<string, ModelFile> ModelCache = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, ModelAnimation> AnimationCache = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, Texture> TextureCache = new( StringComparer.OrdinalIgnoreCase );
	private static Texture? whiteTexture;
	private static Shader? sharedShader;
	private sealed class Part
	{
		public ModelFile.Mesh Mesh = null!;
		public ModelEntity Entity = null!;
		public Model Model = null!;
		public Vertex[] Vertices = null!;
		public Matrix4x4 World;
		public bool Animated;
	}

	private readonly ModelFile modelFile;
	private readonly Part[] parts;
	private ModelAnimation? animation;
	private float startedAt;

	public bool Loop { get; set; } = true;
	public float PlaybackSpeed { get; set; } = 1;
	public bool AnimationFinished { get; private set; }
	public bool Visible { get; set; } = true;

	public LobbyModel( Vector3 position, string modelPath, string textureDirectory, string? animationPath = null, Func<string, Texture?>? textureResolver = null, LobbyQuality? quality = null )
	{
		Position = position;
		textureDirectory = (quality ?? LobbyQuality.Load()).TextureDirectory( textureDirectory );
		Rotation = Quaternion.Identity;
		// A missing model throws with its full path instead of failing later on a null stream.
		modelPath = AnimationFiles.ResolveModel( modelPath );
		if ( !ModelCache.TryGetValue( modelPath, out var loadedModel ) )
			ModelCache[modelPath] = loadedModel = new ModelFile( modelPath );
		modelFile = loadedModel;
		parts = new Part[modelFile.Meshes.Count];

		for ( int meshIndex = 0; meshIndex < parts.Length; meshIndex++ )
		{
			var mesh = modelFile.Meshes[meshIndex];
			var material = new Material<ObjectUniformBuffer>( sharedShader ??= new Shader( "content/shaders/test.shader" ) );
			var textures = new Texture[16];
			for ( int i = 0; i < textures.Length; i++ )
			{
				var name = i < mesh.Materials.Length ? mesh.Materials[i].Name?.TrimEnd( '\0' ) : null;
				if ( string.IsNullOrEmpty( name ) )
					textures[i] = whiteTexture ??= Texture.Missing;
				else
				{
					var texture = textureResolver?.Invoke( name );
					if ( texture == null )
					{
						string texturePath = AnimationFiles.ResolveFile( $"{textureDirectory}/{name}.wct", "Texture" );
						if ( !TextureCache.TryGetValue( texturePath, out texture ) )
							TextureCache[texturePath] = texture = new Texture( texturePath, TextureFlags.Repeat );
					}
					textures[i] = texture;
				}
			}
			material.Set( "Color", textures );

			var vertices = new Vertex[mesh.Vertices.Length];
			for ( int i = 0; i < vertices.Length; i++ )
			{
				int textureIndex = (int)mesh.Vertices[i].TextureIndex;
				vertices[i].TexCoords = mesh.TexCoords[i];
				vertices[i].TexIndex = textureIndex;
				vertices[i].MatFlags = textureIndex < mesh.Materials.Length ? mesh.Materials[textureIndex].Flags : 0;
			}

			var part = new Part { Mesh = mesh, Vertices = vertices, World = mesh.WorldTransform };
			Bake( part, null );
			part.Model = new Model( vertices, mesh.Indices, material );
			part.Entity = new ModelEntity { Model = part.Model, Position = Position, Rotation = Rotation, Scale = Scale };
			parts[meshIndex] = part;
		}

		if ( animationPath != null )
			Play( animationPath );
	}

	/// <summary>
	/// Starts an animation file, normally one returned by <see cref="AnimationFiles.Resolve"/> or
	/// <see cref="AnimationFiles.Segments"/>. A missing file throws with its full path.
	/// </summary>
	public void Play( string animationPath, bool loop = true )
	{
		animationPath = AnimationFiles.ResolveFile( animationPath, "Animation" );
		if ( !AnimationCache.TryGetValue( animationPath, out animation ) )
			AnimationCache[animationPath] = animation = new ModelAnimation( animationPath );
		Loop = loop;
		startedAt = Time.Now;
		AnimationFinished = false;
		OnUpdate();
	}

	private static void Bake( Part part, Vector3[]? positions )
	{
		for ( int i = 0; i < part.Vertices.Length; i++ )
		{
			var source = positions != null ? positions[i] : part.Mesh.Vertices[i].Position;
			var position = System.Numerics.Vector3.Transform( source.GetSystemVector3(), part.World );
			var normal = System.Numerics.Vector3.TransformNormal( part.Mesh.Normals[i].GetSystemVector3(), part.World );
			part.Vertices[i].Position = new Vector3( position.X, position.Z, position.Y );
			part.Vertices[i].Normal = new Vector3( normal.X, normal.Z, normal.Y ).Normal;
		}
	}

	protected override void OnUpdate()
	{
		ModelPose? pose = null;
		if ( animation != null )
		{
			float elapsedFrames = Math.Max( 0, Time.Now - startedAt ) * ModelAnimation.FramesPerSecond * Math.Max( 0, PlaybackSpeed );
			AnimationFinished = !Loop && elapsedFrames >= animation.Length;
			float frame = animation.StartFrame + (Loop && animation.Length > 0 ? elapsedFrames % animation.Length : Math.Min( elapsedFrames, animation.Length ));
			pose = animation.Evaluate( modelFile, frame );
		}

		for ( int i = 0; i < parts.Length; i++ )
		{
			var part = parts[i];
			part.Entity.Position = Position;
			part.Entity.Rotation = Rotation;
			part.Entity.Scale = Scale;
			part.Entity.Model = !Visible || (pose?.Hidden[i] ?? false) ? null : part.Model;
			if ( pose == null )
				continue;

			var positions = pose.Positions[i];
			var texCoords = pose.TexCoords[i];
			bool moved = pose.WorldTransforms[i] != part.World;
			if ( !moved && positions == null && texCoords == null && !part.Animated )
				continue;

			part.World = pose.WorldTransforms[i];
			Bake( part, positions );
			texCoords ??= part.Mesh.TexCoords;
			for ( int j = 0; j < part.Vertices.Length; j++ )
				part.Vertices[j].TexCoords = texCoords[j];
			part.Animated = moved || positions != null || pose.TexCoords[i] != null;
			part.Model.UpdateVertices( part.Vertices );
		}
	}

	protected override void OnDelete()
	{
		foreach ( var part in parts )
		{
			part.Entity.Model = null;
			part.Entity.Delete();
			All.Remove( part.Entity );
		}
	}
}
