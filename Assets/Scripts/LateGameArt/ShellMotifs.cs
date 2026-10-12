using UnityEngine;

// The same polygons define world emblems and HUD emblems; no font glyph dependency.
public static class ShellMotifs
{
    public static readonly Vector2[][] A={new[]{new Vector2(0,.48f),new Vector2(-.46f,-.36f),new Vector2(.46f,-.36f)}};
    public static readonly Vector2[][] B={Diamond(-.27f),Diamond(.27f)};
    static Vector2[] Diamond(float y)=>new[]{new Vector2(0,y+.23f),new Vector2(-.34f,y),new Vector2(0,y-.23f),new Vector2(.34f,y)};
    public static Vector2[][] Polygons(int zone)=>zone==0?A:B;
}


