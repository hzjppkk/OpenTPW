using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyAnimationTests
{
	[TestMethod]
	public void EveryLobbyAnimationSlotEvaluatesWithItsBaseModel()
	{
		GameData.Require();
		var found = new List<string>();
		var pending = new Stack<string>();
		pending.Push( "lobby" );
		while ( pending.Count > 0 )
		{
			var directory = pending.Pop();
			found.AddRange( FileSystem.GetFiles( directory ).Where( p => p.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ) );
			foreach ( var child in FileSystem.GetDirectories( directory ) )
				pending.Push( child );
		}
		var paths = found.ToArray();
		var byName = paths.ToDictionary( p => p.Replace( '\\', '/' ).ToLowerInvariant() );
		int animations = 0, vertexTracks = 0, uvTracks = 0, transformTracks = 0;
		foreach ( var path in paths )
		{
			var match = Regex.Match( Path.GetFileNameWithoutExtension( path ), @"^(.+)[A-Za-z]\d{0,2}$" );
			if ( !match.Success )
				continue;
			string baseName = $"{Path.GetDirectoryName( path )}/{match.Groups[1].Value}.md2".Replace( '\\', '/' ).ToLowerInvariant();
			if ( !byName.TryGetValue( baseName, out var basePath ) )
				continue;

			using var modelStream = FileSystem.OpenRead( basePath );
			using var animationStream = FileSystem.OpenRead( path );
			var model = new ModelFile( modelStream );
			var animation = new ModelAnimation( animationStream );
			if ( animation.Tracks.Count == 0 )
				continue;
			animations++;
			vertexTracks += animation.Tracks.Count( t => t.PackedVertices != null || t.VertexFrames != null );
			uvTracks += animation.Tracks.Count( t => t.TexCoords != null );
			transformTracks += animation.Tracks.Count( t => t.Path != null || t.Rotations.Length > 0 );
			for ( int sample = 0; sample <= 8; sample++ )
			{
				var pose = animation.Evaluate( model, animation.StartFrame + animation.Length * sample / 8 );
				Assert.AreEqual( model.Nodes.Count, pose.WorldTransforms.Length, path );
				Assert.IsFalse( pose.WorldTransforms.Any( m => !float.IsFinite( m.M11 ) || !float.IsFinite( m.M41 ) || !float.IsFinite( m.M42 ) ), path );
				Assert.IsFalse( pose.Positions.Any( p => p != null && p.Any( v => !float.IsFinite( v.X ) || !float.IsFinite( v.Y ) || !float.IsFinite( v.Z ) ) ), path );
				Assert.IsFalse( pose.TexCoords.Any( p => p != null && p.Any( v => !float.IsFinite( v.X ) || !float.IsFinite( v.Y ) ) ), path );
			}
		}
		Console.WriteLine( $"{animations} lobby animations; {vertexTracks} vertex, {uvTracks} UV, {transformTracks} transform tracks" );
		Assert.IsTrue( animations >= 12, "Expected islands, gates and flying meshes" );
		Assert.IsTrue( vertexTracks > 0 );
		Assert.IsTrue( uvTracks > 0 );
		Assert.IsTrue( transformTracks > 0 );
	}
}
