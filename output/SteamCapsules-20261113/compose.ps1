$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
public static class CapsuleType {
 static GraphicsPath Text(string text,FontFamily family) {
  var path=new GraphicsPath();
  path.AddString(text,family,0,1000,new PointF(0,0),StringFormat.GenericTypographic);
  var b=path.GetBounds();var move=new Matrix();move.Translate(-b.X,-b.Y);path.Transform(move);move.Dispose();return path;
 }
 public static void Compose(string source,string font,string dest,string logo,int w,int h,float x,float y,float bw,float bh,bool stacked) {
  using(var fonts=new PrivateFontCollection()) {
   fonts.AddFontFile(font);
   if(fonts.Families[0].Name!="Bungee")throw new Exception("Wrong font: "+fonts.Families[0].Name);
   using(var a=Text("ERI",fonts.Families[0]))using(var b=Text("GONE",fonts.Families[0]))using(var all=new GraphicsPath()) {
    var ab=a.GetBounds();var bb=b.GetBounds();var pos=new Matrix();
    if(stacked){pos.Translate(0,ab.Height+75);b.Transform(pos);pos.Reset();pos.Translate((bb.Width-ab.Width)/2,0);a.Transform(pos);}
    else {pos.Translate(ab.Width+45,0);b.Transform(pos);}
    pos.Dispose();
    var tilt=new Matrix();tilt.Shear(-0.09f,0);tilt.Rotate(-6,MatrixOrder.Append);a.Transform(tilt);b.Transform(tilt);tilt.Dispose();
    all.AddPath(a,false);all.AddPath(b,false);
    var bounds=all.GetBounds();float scale=Math.Min(bw/bounds.Width,bh/bounds.Height)*0.93f;
    var fit=new Matrix(scale,0,0,scale,x+(bw-bounds.Width*scale)/2-bounds.X*scale,y+(bh-bounds.Height*scale)/2-bounds.Y*scale);
    a.Transform(fit);b.Transform(fit);all.Transform(fit);fit.Dispose();
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
     g.DrawImageUnscaled(layer,0,0);
     using(var final=new Bitmap(w,h,PixelFormat.Format24bppRgb))using(var fg=Graphics.FromImage(final)) {
      fg.InterpolationMode=InterpolationMode.HighQualityBicubic;fg.DrawImage(canvas,0,0,w,h);final.Save(dest,ImageFormat.Png);
     }
     using(var final=new Bitmap(w,h,PixelFormat.Format32bppArgb))using(var fg=Graphics.FromImage(final)) {
      fg.InterpolationMode=InterpolationMode.HighQualityBicubic;fg.DrawImage(layer,0,0,w,h);final.Save(logo,ImageFormat.Png);
     }
    }
   }
  }
 }
}
'@
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fontPath = Join-Path $projectRoot 'Assets/Art/UI/ComicToon/Fonts/Bungee-Regular.ttf'
$design = Get-Content (Join-Path $PSScriptRoot 'design.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'PNG'),(Join-Path $PSScriptRoot 'LogoLayers') | Out-Null
foreach($item in $design){
 Copy-Item -LiteralPath $item.source -Destination (Join-Path $PSScriptRoot ('source/'+$item.id+'-art.png'))
 $name=$item.id+'_'+$item.width+'x'+$item.height+'.png'
 [CapsuleType]::Compose($item.source,$fontPath,(Join-Path $PSScriptRoot ('PNG/'+$name)),(Join-Path $PSScriptRoot ('LogoLayers/'+$item.id+'.png')),$item.width,$item.height,$item.box[0],$item.box[1],$item.box[2],$item.box[3],$item.stacked)
 Write-Output $name
}
