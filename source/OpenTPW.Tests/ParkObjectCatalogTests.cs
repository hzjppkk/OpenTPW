using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class ParkObjectCatalogTests
{
	[TestMethod]
	public void InitialParkObjectsHaveModelsAndTextures()
	{
		GameData.Require();
		int checkedSaves = 0;
		var failures = new List<string>();
		foreach ( var level in FileSystem.GetDirectories( "levels" ) )
		{
			var savePath = FileSystem.GetFiles( level ).FirstOrDefault(
				x => Path.GetFileName( x ).Equals( "Easymode.TPWI", StringComparison.OrdinalIgnoreCase ) );
			if ( savePath == null )
				continue;

			checkedSaves++;
			var catalog = new ParkObjectCatalog( Path.GetFileName( level ) );
			var textures = new ParkTextureCatalog( Path.GetFileName( level ) );
			var save = new SaveFile( savePath );
			foreach ( int id in save.Objects.Select( x => x.TypeId ).Distinct() )
			{
				if ( !catalog.Objects.TryGetValue( id, out var definition ) )
				{
					failures.Add( $"{level}: missing type {id}" );
					continue;
				}
				var model = new ModelFile( definition.ModelPath );
				Assert.IsTrue( model.Meshes.Count > 0, definition.ModelPath );
				foreach ( var material in model.Meshes.SelectMany( x => x.Materials ) )
					// Sign textures are composed at runtime from .sgn files.
					if ( material.Name != null && material.Name != "sign1" && material.Name != "sign2"
						&& textures.Find( Path.GetDirectoryName( definition.ModelPath )!, material.Name ) == null )
						failures.Add( $"{definition.ModelPath}: missing texture {material.Name}" );
			}
			// These models already contain their placement relative to the theme's origin.
			foreach ( int id in new[] { 1600, 1601, 1603 } )
				if ( catalog.Objects.TryGetValue( id, out var definition ) )
					Assert.IsTrue( definition.WorldSpace, $"{level}: {id}" );
		}

		Assert.IsTrue( checkedSaves > 0 );
		Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
	}
}
