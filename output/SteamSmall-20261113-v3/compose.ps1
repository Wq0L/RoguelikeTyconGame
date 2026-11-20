$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
public static class CapsuleType {
 static GraphicsPath Poly(RectangleF r,float[] xy,Matrix transform) {
  var p=new GraphicsPath();var points=new PointF[xy.Length/2];
  for(int i=0;i<points.Length;i++)points[i]=new PointF(r.X+xy[i*2]*r.Width,r.Y+xy[i*2+1]*r.Height);
  p.AddPolygon(points);p.Transform(transform);return p;
 }
 static void Fracture(Graphics g,GraphicsPath glyph,RectangleF r,Matrix transform,bool isG,float outline) {
  using(var ink=new SolidBrush(Color.FromArgb(24,13,29)))
  using(var fill=new SolidBrush(isG?Color.FromArgb(255,77,29):Color.FromArgb(255,237,193)))
  using(var shade=new SolidBrush(isG?Color.FromArgb(210,48,24):Color.FromArgb(233,180,117)))
  using(var pen=new Pen(ink,outline*1.25f)) {
   pen.LineJoin=LineJoin.Miter;
   // Deliberately different fracture routes, restricted to the first E and G.
   float[][] cracks=isG?new[]{
    new float[]{.48f,-.05f,.40f,.17f,.53f,.27f,.35f,.43f,.43f,.45f,.61f,.27f,.49f,.16f,.58f,-.05f},
    new float[]{-.05f,.61f,.21f,.69f,.15f,.81f,.35f,1.05f,.43f,1.05f,.23f,.79f,.30f,.66f,-.05f,.54f}
   }:new[]{
    new float[]{-.05f,.29f,.23f,.37f,.14f,.48f,.41f,.58f,.56f,.75f,.61f,.73f,.46f,.54f,.23f,.45f,.31f,.34f,-.05f,.22f},
    new float[]{.28f,-.05f,.35f,.15f,.28f,.25f,.31f,.32f,.40f,.17f,.34f,-.05f}
   };
   var saved=g.Save();g.SetClip(glyph);
   foreach(var xy in cracks)using(var p=Poly(r,xy,transform))g.FillPath(ink,p);
   g.Restore(saved);
   // Remove actual edge wedges from the transparent title layer, then ink their inner edges.
   float[] notch=isG?new float[]{.40f,-.10f,.63f,-.10f,.53f,.13f,.43f,.09f}:new float[]{-.14f,.20f,.04f,.22f,.18f,.30f,.01f,.38f,-.14f,.35f};
   using(var p=Poly(r,notch,transform)) {
    var st=g.Save();g.CompositingMode=CompositingMode.SourceCopy;
    using(var clear=new SolidBrush(Color.Transparent))g.FillPath(clear,p);g.Restore(st);
   }
   float[][] chips=isG?new[]{
    new float[]{.39f,-.23f,.49f,-.28f,.61f,-.18f,.55f,-.08f,.43f,-.09f},
    new float[]{.15f,1.08f,.25f,1.04f,.34f,1.14f,.25f,1.23f,.16f,1.18f}
   }:new[]{
    new float[]{-.23f,.19f,-.11f,.13f,-.04f,.24f,-.10f,.33f,-.23f,.29f},
    new float[]{-.17f,.63f,-.07f,.60f,-.02f,.70f,-.10f,.78f,-.20f,.73f},
    new float[]{.21f,-.17f,.30f,-.22f,.36f,-.11f,.28f,-.05f,.20f,-.09f}
   };
   foreach(var xy in chips)using(var p=Poly(r,xy,transform)) {
    using(var drop=(GraphicsPath)p.Clone()){var m=new Matrix();m.Translate(outline*.5f,outline*1.3f);drop.Transform(m);m.Dispose();g.FillPath(ink,drop);g.DrawPath(pen,drop);}
    g.FillPath(fill,p);g.DrawPath(pen,p);
   }
  }
 }
 static GraphicsPath Text(string text,FontFamily family) {
  var path=new GraphicsPath();
  path.AddString(text,family,0,1000,new PointF(0,0),StringFormat.GenericTypographic);
  var b=path.GetBounds();var move=new Matrix();move.Translate(-b.X,-b.Y);path.Transform(move);move.Dispose();return path;
 }
 public static void Compose(string source,string font,string dest,string logo,int w,int h,float x,float y,float bw,float bh,bool stacked) {
  using(var fonts=new PrivateFontCollection()) {
   fonts.AddFontFile(font);
   if(fonts.Families[0].Name!="Lilita One")throw new Exception("Wrong font: "+fonts.Families[0].Name);
   using(var a=Text("ERI",fonts.Families[0]))using(var b=Text("GONE",fonts.Families[0]))using(var e=Text("E",fonts.Families[0]))using(var gc=Text("G",fonts.Families[0]))using(var all=new GraphicsPath()) {
    var ab=a.GetBounds();var bb=b.GetBounds();var pos=new Matrix();
    if(stacked){pos.Translate(0,ab.Height+95);b.Transform(pos);gc.Transform(pos);pos.Reset();pos.Translate((bb.Width-ab.Width)/2,0);a.Transform(pos);e.Transform(pos);}
    else {pos.Translate(ab.Width+45,0);b.Transform(pos);gc.Transform(pos);}
    pos.Dispose();
    var er=e.GetBounds();var gr=gc.GetBounds();
    var tilt=new Matrix();tilt.Shear(-0.09f,0);tilt.Rotate(-6,MatrixOrder.Append);a.Transform(tilt);b.Transform(tilt);e.Transform(tilt);gc.Transform(tilt);
    all.AddPath(a,false);all.AddPath(b,false);
    var bounds=all.GetBounds();float scale=Math.Min(bw/bounds.Width,bh/bounds.Height)*0.93f;
    var fit=new Matrix(scale,0,0,scale,x+(bw-bounds.Width*scale)/2-bounds.X*scale,y+(bh-bounds.Height*scale)/2-bounds.Y*scale);
    a.Transform(fit);b.Transform(fit);all.Transform(fit);e.Transform(fit);gc.Transform(fit);tilt.Multiply(fit,MatrixOrder.Append);fit.Dispose();
    using(var canvas=new Bitmap(w*2,h*2,PixelFormat.Format32bppArgb))using(var layer=new Bitmap(w*2,h*2,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(canvas))using(var lg=Graphics.FromImage(layer))using(var src=new Bitmap(source)) {
     g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
     float cover=Math.Max((w*2f)/src.Width,(h*2f)/src.Height);
     g.DrawImage(src,(w*2-src.Width*cover)/2,(h*2-src.Height*cover)/2,src.Width*cover,src.Height*cover);
     lg.ScaleTransform(2,2);lg.SmoothingMode=SmoothingMode.AntiAlias;
     float outline=Math.Max(2,27*scale),depth=Math.Max(3,56*scale);
     using(var ink=new SolidBrush(Color.FromArgb(24,13,29)))using(var shadow=new SolidBrush(Color.FromArgb(12,8,20)))using(var cream=new SolidBrush(Color.FromArgb(255,237,193)))using(var orange=new SolidBrush(Color.FromArgb(255,77,29)))using(var pen=new Pen(ink,outline*2)) {
      pen.LineJoin=LineJoin.Round;
      using(var drop=(GraphicsPath)all.Clone()) {var shift=new Matrix();shift.Translate(depth*.55f,depth);drop.Transform(shift);shift.Dispose();lg.DrawPath(pen,drop);lg.FillPath(shadow,drop);}
      lg.DrawPath(pen,all);lg.FillPath(cream,a);lg.FillPath(orange,b);
      using(var creamShade=new SolidBrush(Color.FromArgb(233,180,117)))using(var orangeShade=new SolidBrush(Color.FromArgb(210,48,24))) {
       foreach(var entry in new[]{Tuple.Create(a,creamShade),Tuple.Create(b,orangeShade)}) {
        var rect=entry.Item1.GetBounds();var saved=lg.Save();lg.SetClip(entry.Item1);
        lg.FillPolygon(entry.Item2,new[]{new PointF(rect.Left-10,rect.Top+rect.Height*.81f),new PointF(rect.Right+10,rect.Top+rect.Height*.62f),new PointF(rect.Right+10,rect.Bottom+10),new PointF(rect.Left-10,rect.Bottom+10)});
        lg.Restore(saved);
       }
      }
     }
     Fracture(lg,e,er,tilt,false,outline);Fracture(lg,gc,gr,tilt,true,outline);
     g.DrawImageUnscaled(layer,0,0);
     using(var final=new Bitmap(w,h,PixelFormat.Format24bppRgb))using(var fg=Graphics.FromImage(final)) {
      fg.InterpolationMode=InterpolationMode.HighQualityBicubic;fg.DrawImage(canvas,0,0,w,h);final.Save(dest,ImageFormat.Png);
     }
     using(var final=new Bitmap(w,h,PixelFormat.Format32bppArgb))using(var fg=Graphics.FromImage(final)) {
      fg.InterpolationMode=InterpolationMode.HighQualityBicubic;fg.DrawImage(layer,0,0,w,h);final.Save(logo,ImageFormat.Png);
     }
    }
    tilt.Dispose();
   }
  }
 }
}
'@
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fontPath = Join-Path $projectRoot 'Assets/Art/UI/ComicToon/Fonts/LilitaOne-Regular.ttf'
$design = Get-Content (Join-Path $PSScriptRoot 'design.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'PNG'),(Join-Path $PSScriptRoot 'LogoLayers') | Out-Null
foreach($item in $design){
 $sourcePath = if([System.IO.Path]::IsPathRooted($item.source)){$item.source}else{Join-Path $PSScriptRoot $item.source}
 $copyPath = Join-Path $PSScriptRoot ('source/'+$item.id+'-art.png')
 if([System.IO.Path]::GetFullPath($sourcePath) -ne [System.IO.Path]::GetFullPath($copyPath)){Copy-Item -LiteralPath $sourcePath -Destination $copyPath}
 $name=$item.id+'_'+$item.width+'x'+$item.height+'.png'
 [CapsuleType]::Compose($sourcePath,$fontPath,(Join-Path $PSScriptRoot ('PNG/'+$name)),(Join-Path $PSScriptRoot ('LogoLayers/'+$item.id+'.png')),$item.width,$item.height,$item.box[0],$item.box[1],$item.box[2],$item.box[3],$item.stacked)
 Write-Output $name
}

