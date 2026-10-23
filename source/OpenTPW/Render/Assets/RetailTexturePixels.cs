using StbImageSharp;

namespace OpenTPW;

/// <summary>Pixel storage for native procedural UVs, as produced by 0x571210/0x5717a0.</summary>
internal static class RetailTexturePixels
{
	internal static ImageResult DecodeTga( byte[] bytes )
	{
		var image = ImageResult.FromMemory( bytes, ColorComponents.RedGreenBlueAlpha );
		// Stb returns visual top first; the retail TGA converter stores visual bottom first.
		// Keep this conversion out of shaders and geometry: their native V values stay unchanged.
		int stride = image.Width * 4;
		var row = new byte[stride];
		for ( int y = 0; y < image.Height / 2; y++ )
		{
			int top = y * stride, bottom = (image.Height - 1 - y) * stride;
			Buffer.BlockCopy( image.Data, top, row, 0, stride );
			Buffer.BlockCopy( image.Data, bottom, image.Data, top, stride );
			Buffer.BlockCopy( row, 0, image.Data, bottom, stride );
		}
		return image;
	}
}
