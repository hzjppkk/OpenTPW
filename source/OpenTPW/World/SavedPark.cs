using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Static preview of the World module of a park save.
/// </summary>
public sealed class SavedPark
{
	public SaveFile Save { get; }
	public Vector3 CameraTarget { get; }
	private readonly ParkTextureCatalog textureCatalog;
	private readonly Dictionary<string, Material<ObjectUniformBuffer>> materials = new();
	private Texture? missingTexture;

	public SavedPark( string levelName )
	{
		Save = new SaveFile( $"levels/{levelName}/Easymode.TPWI" );
		var catalog = new ParkObjectCatalog( levelName );
		textureCatalog = new ParkTextureCatalog( levelName );
		var terrain = new ModelFile( $"levels/{levelName}/terrain/base.md2" );
		var heightfield = terrain.Heightfield ?? throw new InvalidDataException( "The level has no heightfield" );
		_ = new Terrain( levelName, Save );
		_ = new Water
		{
			Position = new Vector3( 0, 0, -0.05f ), Scale = new Vector3( 10000f ), Rotation = Quaternion.Identity
		};

		var models = new Dictionary<string, ModelFile>( StringComparer.OrdinalIgnoreCase );
		var positions = new List<Vector3>();
		foreach ( var obj in Save.Objects )
		{
			if ( !catalog.Objects.TryGetValue( obj.TypeId, out var definition ) )
				throw new InvalidDataException( $"Unknown object type {obj.TypeId} in {levelName}" );

			if ( !models.TryGetValue( definition.ModelPath, out var model ) )
				models[definition.ModelPath] = model = new ModelFile( definition.ModelPath );

			var position = Vector3.Zero;
			if ( !definition.WorldSpace )
			{
				float x = obj.X / 256f;
				float y = obj.Y / 256f;
				int cx = Math.Clamp( (int)x, 0, heightfield.Width - 1 );
				int cy = Math.Clamp( (int)y, 0, heightfield.Depth - 1 );
				float fx = x - MathF.Floor( x ), fy = y - MathF.Floor( y );
				float h0 = heightfield.HeightAt( cx, cy ) * (1 - fx) + heightfield.HeightAt( cx + 1, cy ) * fx;
				float h1 = heightfield.HeightAt( cx, cy + 1 ) * (1 - fx) + heightfield.HeightAt( cx + 1, cy + 1 ) * fx;
				position = new Vector3( x * heightfield.CellSizeX, y * heightfield.CellSizeZ, h0 * (1 - fy) + h1 * fy );
				positions.Add( position );
			}

			DrawModel( model, Path.GetDirectoryName( definition.ModelPath )!, position, definition.WorldSpace ? 0 : obj.Angle );
		}

		CameraTarget = positions.Count == 0 ? new Vector3( 480, 425, 0 )
			: positions.Aggregate( Vector3.Zero, (a, b) => a + b ) / positions.Count;
		Log.Trace( $"Loaded {Save.Objects.Count()} park objects from {levelName}/Easymode.TPWI" );
	}

	private void DrawModel( ModelFile file, string directory, Vector3 position, int angle )
	{
		var placement = Matrix4x4.CreateRotationZ( -angle * MathF.PI / 180f )
			* Matrix4x4.CreateTranslation( position.GetSystemVector3() );
		foreach ( var mesh in file.Meshes )
		{
			if ( mesh.Vertices.Length == 0 || mesh.Indices.Length == 0 )
				continue;

			var paths = new string?[16];
			for ( int i = 0; i < paths.Length; i++ )
			{
				string? name = i < mesh.Materials.Length ? mesh.Materials[i].Name : null;
				paths[i] = textureCatalog.Find( directory, name );
			}
			var materialKey = string.Join( "|", paths );
			if ( !materials.TryGetValue( materialKey, out var material ) )
			{
				material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
				material.Set( "Color", paths.Select( path => path == null
					? missingTexture ??= Texture.Missing : new Texture( path, TextureFlags.Repeat ) ).ToArray() );
				materials.Add( materialKey, material );
			}

			var vertices = new Vertex[mesh.Vertices.Length];
			for ( int i = 0; i < vertices.Length; i++ )
			{
				var p = System.Numerics.Vector3.Transform( mesh.Vertices[i].Position.GetSystemVector3(), mesh.WorldTransform );
				var n = System.Numerics.Vector3.TransformNormal( mesh.Normals[i].GetSystemVector3(), mesh.WorldTransform );
				p = System.Numerics.Vector3.Transform( new System.Numerics.Vector3( p.X, p.Z, p.Y ), placement );
				n = System.Numerics.Vector3.TransformNormal( new System.Numerics.Vector3( n.X, n.Z, n.Y ), placement );
				int slot = (int)mesh.Vertices[i].TextureIndex;
				vertices[i] = new Vertex
				{
					Position = new Vector3( p.X, p.Y, p.Z ), Normal = new Vector3( n.X, n.Y, n.Z ).Normal,
					TexCoords = mesh.TexCoords[i], TexIndex = slot, MatFlags = mesh.Materials[slot].Flags
				};
			}

			_ = new ModelEntity
			{
				// Bake static placement so shared materials use the same model uniform.
				Model = new Model( vertices, mesh.Indices, material ), Rotation = Quaternion.Identity
			};
		}
	}
}
