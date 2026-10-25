namespace OpenTPW;

/// <summary>Partitions complete triangles to fit the world shader's sixteen texture bindings.</summary>
internal static class LobbyModelBatches
{
	internal sealed record Batch( int FirstMaterial, uint[] Indices );
	public static IReadOnlyList<Batch> Create( ModelFile.Mesh mesh )
	{
		var batches = new SortedDictionary<int, List<uint>>();
		if ( mesh.Indices.Length % 3 != 0 ) throw new InvalidDataException( "Incomplete model triangle" );
		for ( int face = 0; face < mesh.Indices.Length; face += 3 )
		{
			uint a = mesh.Indices[face], b = mesh.Indices[face + 1], c = mesh.Indices[face + 2];
			uint material = mesh.Vertices[a].TextureIndex;
			if ( material >= mesh.Materials.Length || mesh.Vertices[b].TextureIndex != material || mesh.Vertices[c].TextureIndex != material )
				throw new InvalidDataException( "Model triangle has inconsistent material indices" );
			int start = (int)material / 16 * 16;
			if ( !batches.TryGetValue( start, out var indices ) ) batches.Add( start, indices = new() );
			indices.Add( a ); indices.Add( b ); indices.Add( c );
		}
		return batches.Select( pair => new Batch( pair.Key, pair.Value.ToArray() ) ).ToArray();
	}
}
