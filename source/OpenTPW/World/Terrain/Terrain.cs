﻿namespace OpenTPW;

/// <summary>
/// The ground of a level, built from the heightfield in its terrain/base.md2.
/// </summary>
public class Terrain : ModelEntity
{
	public Terrain( string levelName, SaveFile? save = null )
	{
		var modelFile = new ModelFile( $"levels/{levelName}/terrain/base.md2" );
		var heightfield = modelFile.Heightfield;
		if ( heightfield == null )
			return;

		// Each ground texture the level uses gets one of the shader's 16 slots
		var slots = new Dictionary<int, int>();
		var textures = new List<Texture>();
		for ( int z = 0; z < heightfield.Depth; z++ )
		{
			for ( int x = 0; x < heightfield.Width; x++ )
			{
				int id = TextureAt( x, z );
				if ( IsWater( x, z ) || slots.ContainsKey( id ) || slots.Count == 16 )
					continue;

				slots[id] = slots.Count;
				textures.Add( id < modelFile.TextureNames.Length
					? new Texture( $"levels/{levelName}/terrain/textures/{modelFile.TextureNames[id]}.wct", TextureFlags.Repeat )
					: Texture.Missing );
			}
		}

		while ( textures.Count < 16 )
			textures.Add( Texture.Missing );

		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
		material.Set( "Color", [.. textures] );

		// One quad per cell, each with the whole texture. Water cells (rivers, lakes) have no
		// ground texture; the water surface is drawn separately
		var vertices = new List<Vertex>();
		var indices = new List<uint>();
		for ( int z = 0; z < heightfield.Depth; z++ )
		{
			for ( int x = 0; x < heightfield.Width; x++ )
			{
				if ( IsWater( x, z ) )
					continue;

				int slot = slots.GetValueOrDefault( TextureAt( x, z ) );
				uint first = (uint)vertices.Count;

				vertices.Add( Corner( heightfield, x, z, new Vector2( 0, 0 ), slot ) );
				vertices.Add( Corner( heightfield, x + 1, z, new Vector2( 1, 0 ), slot ) );
				vertices.Add( Corner( heightfield, x + 1, z + 1, new Vector2( 1, 1 ), slot ) );
				vertices.Add( Corner( heightfield, x, z + 1, new Vector2( 0, 1 ), slot ) );

				indices.AddRange( [first, first + 2, first + 1, first, first + 3, first + 2] );
			}
		}

		Model = new Model( [.. vertices], [.. indices], material );
		Rotation = System.Numerics.Quaternion.Identity;

		bool IsWater( int x, int z ) => heightfield.IsWater( x, z )
			|| (save != null && save.Map[z * SaveFile.MapSize + x].Type == 2);

		int TextureAt( int x, int z )
		{
			if ( save != null && save.Map[z * SaveFile.MapSize + x].Type == 1 )
			{
				int road = Array.FindIndex( modelFile.TextureNames, name => name.Equals( "grd_ctr1", StringComparison.OrdinalIgnoreCase ) );
				if ( road >= 0 )
					return road;
			}
			return heightfield.TextureAt( x, z );
		}
	}

	/// <summary>
	/// A corner in engine axes (X, Z, Y up in the game becomes X, Y, Z up here).
	/// </summary>
	private static Vertex Corner( ModelFile.HeightfieldData heightfield, int x, int z, Vector2 texCoords, int slot )
	{
		float HeightAt( int cx, int cz ) => heightfield.HeightAt( Math.Clamp( cx, 0, heightfield.Width ), Math.Clamp( cz, 0, heightfield.Depth ) );

		var normal = new Vector3(
			(HeightAt( x - 1, z ) - HeightAt( x + 1, z )) / (2 * heightfield.CellSizeX),
			(HeightAt( x, z - 1 ) - HeightAt( x, z + 1 )) / (2 * heightfield.CellSizeZ),
			1 ).Normal;

		return new Vertex()
		{
			Position = new Vector3( x * heightfield.CellSizeX, z * heightfield.CellSizeZ, heightfield.HeightAt( x, z ) ),
			Normal = normal,
			TexCoords = texCoords,
			TexIndex = slot
		};
	}
}
