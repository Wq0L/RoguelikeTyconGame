-- Original titanium controls, drawn and exported in Aseprite.
local out=assert(app.params.output)
local preview=assert(app.params.preview)
local W,H=128,32
local function C(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local function rect(im,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,c) end end
end
local function inside(x,y,l,t,r,b)
  if x<l or x>r or y<t or y>b then return false end
  local dy=math.min(y-t,b-y)
  return math.min(x-l,r-x)>=({3,1,0})[math.min(dy+1,3)]
end
local names={'Normal','Hover','Pressed','Disabled'}
local sheet=Image(544,176,ColorMode.RGB)
rect(sheet,0,0,544,176,C(19,28,39))
local sprite=Sprite(W,H,ColorMode.RGB)
sprite.layers[1].name='Metal control states'
for i,name in ipairs(names) do
  local im=Image(W,H,ColorMode.RGB)
  local pressed=name=='Pressed'
  local disabled=name=='Disabled'
  local top=pressed and 4 or 2
  local bottom=pressed and 28 or 26
  local accent=disabled and C(91,108,117) or (name=='Hover' and C(150,249,248) or C(76,196,202))
  for y=0,H-1 do for x=0,W-1 do
    local c
    if inside(x,y,1,4,126,30) then c=C(10,18,27,200) end
    if inside(x,y,1,top,126,bottom) then
      c=disabled and C(122,139,151) or C(194,210,221)
      if not inside(x,y,2,top+1,125,bottom-1) then c=C(55,72,87)
      elseif y==top+1 then c=pressed and C(105,125,142) or C(242,248,250)
      elseif y>=bottom-2 then c=pressed and C(212,229,236) or C(110,132,150)
      elseif x<6 or x>121 then c=C(148,169,185) end
      if inside(x,y,10,top+5,117,bottom-4) then
        c=disabled and C(43,52,63) or C(31,48,65)
        if y==top+5 then c=C(12,23,34)
        elseif y==top+6 then c=C(23,36,49)
        elseif y==bottom-4 then c=C(75,99,117) end
      end
    end
    if c then im:drawPixel(x,y,c) end
  end end
  -- Side fasteners remain inside the protected nine-slice corners.
  for _,x in ipairs({5,120}) do
    rect(im,x,top+9,3,5,C(76,95,111))
    rect(im,x,top+9,2,1,C(237,244,247))
    rect(im,x+1,top+11,1,2,C(27,43,57))
  end
  rect(im,13,top+3,6,1,accent)
  rect(im,109,top+3,6,1,accent)
  if i>1 then sprite:newEmptyFrame() end
  sprite:newCel(sprite.layers[1],i,im,Point(0,0))
  sprite.frames[i].duration=0.15
  local tag=sprite:newTag(i,i); tag.name=name
  im:saveAs(out..'/Button_'..name..'.png')
  local px=((i-1)%2)*272+16
  local py=math.floor((i-1)/2)*88+12
  for y=0,H-1 do for x=0,W-1 do
    local c=im:getPixel(x,y)
    if app.pixelColor.rgbaA(c)>0 then rect(sheet,px+x*2,py+y*2,2,2,c) end
  end end
end
local slice=sprite:newSlice(Rectangle(0,0,W,H))
slice.name='Button'; slice.center=Rectangle(20,10,88,10)
sprite:saveAs(out..'/SciFi_Button.aseprite')
sheet:saveAs(preview..'/Button_States_Preview.png')
app.exit()
