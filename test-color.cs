using System;
using FreeTypeSharp.Native;

// Quick test to check if FreeType 2.10.1 renders color emoji from Segoe UI Emoji
unsafe
{
    FT_Error err = FT.FT_Init_FreeType(out nint lib);
    if (err != FT_Error.FT_Err_Ok) { Console.WriteLine("FT_Init_FreeType failed"); return; }

    string fontPath = @"C:\Windows\Fonts\seguiemj.ttf";
    err = FT.FT_New_Face(lib, fontPath, 0, out nint face);
    if (err != FT_Error.FT_Err_Ok) { Console.WriteLine($"FT_New_Face failed: {err}"); return; }

    FT.FT_Set_Char_Size(face, 0, 13 * 64, 96, 96);

    var f = (FT_FaceRec*)face;
    Console.WriteLine($"Family: {f->family_name}");
    Console.WriteLine($"Has color: {(f->face_flags & FT.FT_FACE_FLAG_COLOR) != 0}");
    Console.WriteLine($"Num glyphs: {f->num_glyphs}");

    // Load a smiley face emoji (U+1F600)
    uint charCode = 0x1F600;
    uint glyphIndex = FT.FT_Get_Char_Index(face, charCode);
    Console.WriteLine($"Glyph index for U+1F600: {glyphIndex}");

    if (glyphIndex > 0)
    {
        err = FT.FT_Load_Glyph(face, glyphIndex, FT.FT_LOAD_DEFAULT | FT.FT_LOAD_COLOR);
        if (err == FT_Error.FT_Err_Ok)
        {
            var slot = f->glyph;
            err = FT.FT_Render_Glyph((nint)slot, FT_Render_Mode.FT_RENDER_MODE_NORMAL);
            if (err == FT_Error.FT_Err_Ok)
            {
                var bitmap = slot->bitmap;
                Console.WriteLine($"Bitmap: {bitmap.width}x{bitmap.rows}");
                Console.WriteLine($"Pixel mode: {(FT_Pixel_Mode)bitmap.pixel_mode}");
                Console.WriteLine($"Pitch: {bitmap.pitch}");

                if ((FT_Pixel_Mode)bitmap.pixel_mode == FT_Pixel_Mode.FT_PIXEL_MODE_BGRA)
                {
                    Console.WriteLine("BGRA output!");
                    // Check first pixel
                    byte* buf = (byte*)bitmap.buffer;
                    Console.WriteLine($"First pixel: B={buf[0]}, G={buf[1]}, R={buf[2]}, A={buf[3]}");
                }
            }
            else
            {
                Console.WriteLine($"FT_Render_Glyph failed: {err}");
            }
        }
        else
        {
            Console.WriteLine($"FT_Load_Glyph failed: {err}");
        }
    }

    FT.FT_Done_Face(face);
    FT.FT_Done_FreeType(lib);
}
