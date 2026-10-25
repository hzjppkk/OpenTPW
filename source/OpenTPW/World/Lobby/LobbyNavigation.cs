using System.Globalization;

namespace OpenTPW;

/// <summary>Normal local lobby selection and key checks (0x5dc5f0, 0x5dc6e0, 0x5dcb90).</summary>
public sealed class LobbyNavigation
{
	private readonly LobbyConfig config;
	private readonly Dictionary<int, int> costs = new();
	private PlayerProfileFile? profile;
	private int index;
	public bool Active => profile != null;
	public bool Entering { get; private set; }
	public LobbyIslandConfig Island => config.Islands[index];
	public int Keys => profile == null ? 0 : LobbyPlayerProgress.KeyCount( profile, config.Islands.Select( island => island.ThemeName ) );
	public int RequiredKeys => costs[Island.Id];
	public bool CanEnter => Active && !Entering && Keys >= RequiredKeys;
	public event Action<int>? IslandSelected;
	public event Action? EntryRequested;
	public event Action? EntryCancelled;
	public LobbyNavigation( LobbyConfig config )
	{
		this.config = config;
		foreach ( var island in config.Islands )
		{
			var settings = new SettingsFile( $"levels/{island.ThemeName}/global.sam" );
			if ( !int.TryParse( settings["Keys.CostToEnter"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost ) || cost < 1 || cost > 5 )
				throw new InvalidDataException( $"Invalid Keys.CostToEnter for {island.ThemeName}" );
			costs.Add( island.Id, cost );
		}
	}
	public void Bind( PlayerProfileFile player )
	{
		// A new player selection starts at the first configured island, never a stale entry state.
		profile = player ?? throw new ArgumentNullException( nameof(player) );
		Entering = false;
		index = 0;
		IslandSelected?.Invoke( Island.Id );
	}
	public void Navigate( int direction )
	{
		if ( !Active || Entering ) return;
		// Normalize negative modulo as well: the previous-island control wraps at index zero.
		index = ((index + direction) % config.Islands.Count + config.Islands.Count) % config.Islands.Count;
		IslandSelected?.Invoke( Island.Id );
	}
	public bool Enter()
	{
		// Lock selection before notifying the camera so repeated input cannot queue two entries.
		if ( !CanEnter ) return false;
		Entering = true;
		EntryRequested?.Invoke();
		return true;
	}
	public bool CancelEntry()
	{
		if ( !Entering ) return false;
		Entering = false;
		EntryCancelled?.Invoke();
		return true;
	}
	public void Unbind()
	{
		CancelEntry();
		profile = null;
	}
}
