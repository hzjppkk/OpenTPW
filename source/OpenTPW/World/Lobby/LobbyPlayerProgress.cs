using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>Lobby key accounting at 0x5abaa0 and the new-player grant at 0x4a44e0/0x5ac050.</summary>
public static class LobbyPlayerProgress
{
	public static int KeyCount( PlayerProfileFile profile, IEnumerable<string> themes )
	{
		var available = themes.ToHashSet( StringComparer.OrdinalIgnoreCase );
		int awards = profile.Prefix.Take( 6 ).Count( value => value != 0 );
		foreach ( var segment in profile.Segments )
			if ( available.Contains( segment.Name.TrimEnd( '\0' ) ) ) awards += segment.Data.Take( 6 ).Count( value => value != 0 );
		return checked(awards / 3 + BinaryPrimitives.ReadInt32LittleEndian( profile.Prefix.AsSpan( 10, 4 ) ));
	}
	public static void GrantIntroductionKey( PlayerProfileFile profile )
	{
		var field = profile.Prefix.AsSpan( 10, 4 );
		BinaryPrimitives.WriteInt32LittleEndian( field, checked(BinaryPrimitives.ReadInt32LittleEndian( field ) + 1) );
	}
}
