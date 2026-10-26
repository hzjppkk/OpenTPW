using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class GameLanguageTests
{
	[TestMethod]
	public void RequestedFolderKeepsItsSpellingOnDisk()
	{
		Assert.AreEqual( "TraditionalChinese", GameLanguage.Resolve( new[] { "English", "TraditionalChinese" }, "traditionalchinese" ) );
	}

	[TestMethod]
	public void WithoutRequestEnglishWinsThenTheOnlyFolder()
	{
		Assert.AreEqual( "English", GameLanguage.Resolve( new[] { "TraditionalChinese", "English" }, "" ) );
		Assert.AreEqual( "TraditionalChinese", GameLanguage.Resolve( new[] { "TraditionalChinese" }, null ) );
	}

	[TestMethod]
	public void MissingOrAmbiguousLanguageFails()
	{
		Assert.ThrowsException<DirectoryNotFoundException>( () => GameLanguage.Resolve( new[] { "English" }, "French" ) );
		Assert.ThrowsException<DirectoryNotFoundException>( () => GameLanguage.Resolve( new[] { "French", "German" }, null ) );
		Assert.ThrowsException<DirectoryNotFoundException>( () => GameLanguage.Resolve( new string[0], null ) );
	}
}
