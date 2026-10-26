using System.Text;

namespace OpenTPW;

public static class Localization
{
	private static StringFile UIStrings;

	static Localization()
	{
		UIStrings = new StringFile( GameLanguage.Path( "UITEXT.str" ) );
	}

	/// <summary>
	/// File index of a <see cref="OpenTPW.UIStrings"/> value. The enum follows the 473-entry tables (v1 data:
	/// TW, CN, the euro CD). Version 2.0 tables (US install, retail language packs) insert one string at 207
	/// ("You cannot load this park, as it uses new rides..."), and the US exe pushes 0xf3 where TW pushes
	/// 0xf2 for TypeYourName (TW 0x4a49f8, US 0x4a6f78).
	/// </summary>
	public static int FileIndex( UIStrings value, int count ) => count == 474 && (int)value >= 207 ? (int)value + 1 : (int)value;

	private class LocalizationParser : BaseParser
	{
		public LocalizationParser( string input ) : base( input ) { }

		public string Parse()
		{
			var sb = new StringBuilder();

			while ( !EndOfFile() )
			{
				sb.Append( ConsumeWhile( c => c != '#' ) );

				if ( !EndOfFile() )
				{
					ConsumeChar(); // #

					var key = ConsumeWhile( x => !char.IsWhiteSpace( x ) );
					key = key.Trim();

					var enumVal = Enum.Parse<UIStrings>( key );
					sb.Append( UIStrings[FileIndex( enumVal, UIStrings.Entries.Length )] );
				}
			}

			return sb.ToString();
		}
	}

	public static string Parse( string str )
	{
		var parser = new LocalizationParser( str );
		return parser.Parse();
	}
}
