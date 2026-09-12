-- Aseprite --batch --script-param output=... --script create_cards.lua
local out=assert(app.params.output)
local previewOut=assert(app.params.preview)
local W,H=112,160
local function c(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local steel=c(233,242,249)
local function rect(im,x,y,w,h,col)
  for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,col) end end
end
local function rr(x,y,l,t,r,b,rad)
  if x<l or x>r or y<t or y>b then return false end
  local dx=math.min(x-l,r-x); local dy=math.min(y-t,b-y)
  local cut=({[0]={},[1]={1},[2]={2,1},[3]={3,1,1},[4]={4,2,1,1},[5]={5,3,2,1,1}})[rad]
  return dy>=rad or dx>=cut[dy+1]
end
local function imagefn(fn)
  local im=Image(W,H,ColorMode.RGB)
  for y=0,H-1 do for x=0,W-1 do local col=fn(x,y); if col then im:drawPixel(x,y,col) end end end
  return im
end
local function add(sp,name,im,first,visible)
  local ly=first and sp.layers[1] or sp:newLayer(); ly.name=name
  sp:newCel(ly,1,im,Point(0,0)); ly.isVisible=visible~=false
end
local function save(im,name) im:saveAs(out..'/'..name..'.png') end
local shadow=imagefn(function(x,y)
  if rr(x,y,2,5,109,159,5) and not rr(x,y,2,2,109,156,5) then return c(12,19,29,y==159 and 25 or 65) end
end)
local frame=imagefn(function(x,y)
  if not rr(x,y,2,2,109,156,5) or rr(x,y,7,7,104,151,2) then return end
  if not rr(x,y,3,3,108,155,4) then return c(69,84,103) end
  if not rr(x,y,4,4,107,154,3) then return y<80 and c(255,255,255) or c(157,176,195) end
  if rr(x,y,6,6,105,152,3) then return y<80 and c(121,143,165) or c(250,252,255) end
  return y>=153 and c(190,207,220) or steel
end)
local background=imagefn(function(x,y)
  if not rr(x,y,7,7,104,151,2) then return end
  if y==7 or x==7 then return c(12,23,36) end
  if y==151 then return c(65,85,103) end
  return c(29,43,60)
end)
local layout=Image(W,H,ColorMode.RGB)
rect(layout,12,12,88,17,c(42,60,78))
rect(layout,12,28,88,1,c(69,89,109))
rect(layout,12,35,88,64,c(15,28,42))
rect(layout,13,36,86,1,c(10,20,33))
rect(layout,13,98,86,1,c(57,78,98))
-- Quiet segmented grid inside the illustration bay.
for yy=43,92,8 do for xx=20,92,8 do layout:drawPixel(xx,yy,c(28,43,59)) end end
-- Corner brackets and tiny mechanical breaks.
for _,p in ipairs({{16,39},{91,39},{16,91},{91,91}}) do
  rect(layout,p[1],p[2],5,1,c(67,90,110)); rect(layout,p[1],p[2],1,4,c(67,90,110))
end
rect(layout,14,107,84,1,c(56,76,95))
rect(layout,14,130,84,1,c(56,76,95))
rect(layout,12,135,88,12,c(21,33,48))
for xx=18,30,4 do rect(layout,xx,150,2,1,c(104,128,150)) end
rect(layout,87,150,7,1,c(104,128,150))
local hover=imagefn(function(x,y)
  if rr(x,y,1,1,110,157,5) and not rr(x,y,2,2,109,156,5) then return c(167,235,255) end
end)
local selected=imagefn(function(x,y)
  if rr(x,y,0,0,111,158,5) and not rr(x,y,2,2,109,156,5) then return c(111,234,255) end
end)
rect(selected,88,135,11,11,c(80,204,227))
for i=0,2 do rect(selected,90+i,140+i,1,2,c(13,40,54)) end
for i=0,4 do rect(selected,92+i,142-i,1,2,c(13,40,54)) end
save(frame,'Card_Frame'); save(background,'Card_Background'); save(layout,'Card_Layout')
save(shadow,'Card_Shadow'); save(hover,'Card_Hover'); save(selected,'Card_Selected')
local specs={{'Common',c(164,188,203)},{'Rare',c(86,212,240)},{'Epic',c(189,144,250)},{'Legendary',c(247,196,99)}}
local cards={}
for n,spec in ipairs(specs) do
  local accent=Image(W,H,ColorMode.RGB)
  rect(accent,17,4,25,1,spec[2]); rect(accent,44,4,6,1,spec[2])
  rect(accent,3,44,1,18,spec[2]); rect(accent,108,44,1,18,spec[2])
  rect(accent,15,16,2,9,spec[2]); rect(accent,14,107,20,1,spec[2])
  for i=1,n do rect(accent,16+(i-1)*5,140,3,3,spec[2]) end
  local sp=Sprite(W,H,ColorMode.RGB)
  add(sp,'Shadow',shadow,true); add(sp,'Background',background); add(sp,'Frame',frame)
  add(sp,'Layout',layout); add(sp,'Rarity '..spec[1],accent)
  local flat=Image(W,H,ColorMode.RGB); flat:drawSprite(sp,1)
  save(flat,'Card_'..spec[1]); save(accent,'Accent_'..spec[1]); cards[n]=flat
  add(sp,'Hover (toggle)',hover,false,false); add(sp,'Selected (toggle)',selected,false,false)
  local slice=sp:newSlice(Rectangle(0,0,W,H)); slice.name='Outer_Frame_Only'
  slice.center=Rectangle(10,10,92,140)
  sp:saveAs(out..'/Card_'..spec[1]..'.aseprite')
end
-- Six original 24px mutation glyphs. Silhouettes remain distinct without color.
local types={'Fertile','Water','Crystal','Energy','Explosive','Duplicate'}
local colors={c(143,227,175),c(101,205,244),c(185,164,255),c(248,212,121),c(250,150,121),c(137,218,227)}
local icons={}
for n,name in ipairs(types) do
  local im=Image(24,24,ColorMode.RGB)
  local function shape(x,y)
    if n==1 then return (x>=11 and x<=12 and y>=10 and y<=20) or (y>=5 and y<=12 and x>=4 and x<=10 and math.abs(x-7)+math.abs(y-8)<=5) or (y>=3 and y<=10 and x>=13 and x<=20 and math.abs(x-16)+math.abs(y-6)<=5) or (y==20 and x>=7 and x<=17)
    elseif n==2 then return y>=3 and y<=20 and math.abs(x-12)<=math.min(math.floor((y-2)*0.65),math.floor((23-y)*1.5),7)
    elseif n==3 then return y>=2 and y<=21 and math.abs(x-12)<=math.floor(math.min((y-1)*0.8,(22-y)*0.8,7))
    elseif n==4 then return (y>=2 and y<=12 and x>=13-math.floor(y*0.65) and x<=17-math.floor(y*0.4)) or (y>=11 and y<=21 and x>=12-math.floor((y-11)*0.3) and x<=18-math.floor((y-11)*0.9))
    elseif n==5 then return (math.abs(x-12)+math.abs(y-12)<=8) or (x>=11 and x<=13 and y>=2 and y<=22) or (y>=11 and y<=13 and x>=2 and x<=22) or ((x==5 or x==19) and (y==5 or y==19))
    else return (x>=4 and x<=14 and y>=4 and y<=14 and (x<=5 or x>=13 or y<=5 or y>=13)) or (x>=10 and x<=20 and y>=10 and y<=20 and (x<=11 or x>=19 or y<=11 or y>=19)) end
  end
  for y=0,23 do for x=0,23 do if shape(x,y) then
    local col=colors[n]
    if not shape(x,y-1) or not shape(x-1,y) then col=c(225,248,255) end
    im:drawPixel(x,y,col)
  end end end
  icons[n]=im; save(im,'Icon_'..name)
end
local iconsp=Sprite(24,24,ColorMode.RGB)
for n,name in ipairs(types) do add(iconsp,name,icons[n],n==1,n==1) end
iconsp:saveAs(out..'/Mutation_Icons.aseprite')
-- Preview text is deliberately separate from all exported game sprites.
local font={A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},L={'100','100','100','100','111'},M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},V={'101','101','101','101','010'},W={'101','101','111','111','101'},X={'101','101','010','101','101'},Y={'101','101','010','010','010'},[' ']={'000','000','000','000','000'}}
local function label(im,str,x,y,col)
  for ch in str:upper():gmatch('.') do local glyph=font[ch]; if glyph then
    for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then im:drawPixel(x+xx-1,y+yy-1,col) end end end
  end; x=x+4 end
end
local sheet=Image(504,224,ColorMode.RGB); sheet:clear(c(17,25,38))
label(sheet,'MUTATION CARDS',14,8,c(210,227,239))
local demo={1,2,3,4}
for n,spec in ipairs(specs) do
  local x=12+(n-1)*124
  sheet:drawImage(cards[n],Point(x,23))
  label(sheet,types[demo[n]],x+23,42,c(229,241,249))
  label(sheet,spec[1],x+41,162,spec[2])
  local big=Image(48,48,ColorMode.RGB)
  for yy=0,47 do for xx=0,47 do big:drawPixel(xx,yy,icons[demo[n]]:getPixel(math.floor(xx/2),math.floor(yy/2))) end end
  sheet:drawImage(big,Point(x+32,66))
  -- Sample copy placement only, no fabricated stat values.
  label(sheet,'MUTATION',x+17,139,c(154,177,198))
end
for n,name in ipairs(types) do
  local x=14+(n-1)*82
  sheet:drawImage(icons[n],Point(x,190)); label(sheet,name,x+27,200,c(160,184,204))
end
local scaled=Image(1008,448,ColorMode.RGB)
for y=0,447 do for x=0,1007 do scaled:drawPixel(x,y,sheet:getPixel(math.floor(x/2),math.floor(y/2))) end end
scaled:saveAs(previewOut..'/Cards_Preview.png')
print('Exported 4 card rarities, 6 glyphs, separate layers and state overlays.')
