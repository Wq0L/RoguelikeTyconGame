-- Individually machined rarity plates sharing Common's titanium material.
local out=assert(app.params.output)
local preview=assert(app.params.preview)
local rarity=assert(app.params.rarity)
assert(rarity=='Rare' or rarity=='Epic' or rarity=='Legendary')
local W,H=128,180
local function C(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local function box(im,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,c) end end
end
local function rounded(x,y,l,t,r,b,rad)
  if x<l or x>r or y<t or y>b then return false end
  local cut=({[0]={},[1]={1},[2]={2,1},[3]={3,1,1},[4]={4,2,1,1},[5]={5,3,2,1,1},[6]={6,4,3,2,1,1}})[rad]
  local dy=math.min(y-t,b-y)
  return dy>=rad or math.min(x-l,r-x)>=cut[dy+1]
end
local function silhouette(x,y)
  if rarity=='Rare' then
    return rounded(x,y,6,8,121,173,6)
      or rounded(x,y,32,3,95,16,3)
      or rounded(x,y,2,45,10,70,3)
      or rounded(x,y,117,45,125,70,3)
  elseif rarity=='Epic' then
    return rounded(x,y,6,10,121,172,6)
      or rounded(x,y,42,2,85,17,4)
      or rounded(x,y,1,37,11,77,3)
      or rounded(x,y,116,37,126,77,3)
      or rounded(x,y,2,119,11,145,3)
      or rounded(x,y,116,119,125,145,3)
  end
  return rounded(x,y,6,12,121,171,6)
    or rounded(x,y,25,5,102,24,4)
    or rounded(x,y,49,0,78,18,4)
    or rounded(x,y,1,28,12,56,3)
    or rounded(x,y,115,28,126,56,3)
    or rounded(x,y,2,135,14,160,3)
    or rounded(x,y,113,135,125,160,3)
    or rounded(x,y,43,163,84,176,3)
end
local function opening(x,y) return rounded(x,y,16,29,111,151,4) end
local function mapped(fn)
  local im=Image(W,H,ColorMode.RGB)
  for y=0,H-1 do for x=0,W-1 do local c=fn(x,y); if c then im:drawPixel(x,y,c) end end end
  return im
end
local shadow=mapped(function(x,y)
  if silhouette(x-1,y-3) and not silhouette(x,y) then return C(10,18,28,65) end
end)
local plate=mapped(function(x,y)
  if not silhouette(x,y) or opening(x,y) then return end
  if not silhouette(x-1,y) or not silhouette(x+1,y) or not silhouette(x,y-1) or not silhouette(x,y+1) then return C(60,72,87) end
  if not silhouette(x-1,y-1) or not silhouette(x,y-2) then return C(245,250,251) end
  if not silhouette(x+1,y+1) or not silhouette(x,y+2) then return C(127,146,162) end
  if not silhouette(x+2,y+2) then return C(167,184,195) end
  if opening(x+1,y) or opening(x,y+1) then return C(94,115,132) end
  if opening(x-1,y) or opening(x,y-1) then return C(242,248,250) end
  if x<=11 then return C(162,179,192) end
  if x>=117 then return C(183,199,210) end
  if y<=10 then return C(228,237,242) end
  if y>=167 then return C(171,188,201) end
  return C(207,220,229)
end)
local background=mapped(function(x,y)
  if not opening(x,y) then return end
  if not opening(x-1,y) or not opening(x,y-1) then return C(15,27,39) end
  if not opening(x,y-2) then return C(24,38,52) end
  if not opening(x,y+1) then return C(60,79,96) end
  return C(34,49,65)
end)
local details=Image(W,H,ColorMode.RGB)
local glow = rarity=='Rare' and C(108,216,234) or rarity=='Epic' and C(188,159,235) or C(236,198,119)
local bright = rarity=='Rare' and C(211,252,255) or rarity=='Epic' and C(237,224,255) or C(255,240,194)
local dark = rarity=='Rare' and C(61,107,131) or rarity=='Epic' and C(104,85,141) or C(139,110,68)
local function inset(x,y,w,h)
  box(details,x,y,w,h,C(121,141,157))
  box(details,x+1,y+1,w-2,h-2,C(172,190,203))
  box(details,x+1,y+h-1,w-2,1,C(244,249,251))
end
-- Integrated blank identification plate, with a narrow inset channel.
inset(26,12,76,11)
box(details,28,14,72,6,C(66,84,102))
box(details,29,15,70,1,C(49,67,83))
box(details,29,20,70,1,C(193,213,225))
-- Broad corner plates use the same alloy, not a colored outline.
local function screw(x,y)
  box(details,x+1,y,3,1,C(119,137,151))
  box(details,x,y+1,5,3,C(119,137,151))
  box(details,x+1,y+4,3,1,C(245,250,252))
  box(details,x+1,y+1,3,3,C(228,237,241))
  box(details,x+1,y+2,3,1,C(93,114,131))
end
screw(14,16); screw(109,16); screw(14,159); screw(109,159)
-- Recessed right-edge grips, with restrained edge reflections.
for _,y in ipairs({49,56,63,117,124,131}) do
  box(details,115,y,3,2,C(124,146,163))
  box(details,115,y+2,3,1,C(236,243,247))
end
-- Lower latch, service grille, and a single status lamp.
inset(29,157,48,9)
for x=33,69,6 do box(details,x,159,3,4,C(74,95,112)); box(details,x,163,3,1,C(228,239,245)) end
inset(83,157,16,9)
box(details,86,160,4,3,C(84,111,125))
box(details,91,160,5,3,glow)
box(details,91,160,5,1,bright)
-- Interrupted milled seam at the shoulders, kept away from the empty interior.
box(details,13,25,16,1,C(139,160,177)); box(details,99,25,16,1,C(139,160,177))
box(details,13,26,15,1,C(243,249,252)); box(details,100,26,15,1,C(243,249,252))
box(details,28,169,25,1,C(135,157,176)); box(details,56,169,5,1,C(135,157,176))
-- Rarity-specific assemblies. No shared colored outline substitutes for shape.
local function module(x,y,w,h)
  box(details,x,y,w,h,C(105,126,144))
  box(details,x+1,y+1,w-2,h-2,C(184,203,216))
  box(details,x+1,y+1,w-2,1,C(245,251,255))
  box(details,x+w-2,y+2,1,h-3,C(146,168,185))
end
if rarity=='Rare' then
  -- Twin narrow coolant couplers and a raised calibration rail.
  module(36,5,56,5)
  box(details,42,7,28,1,C(73,96,117))
  box(details,74,7,5,1,glow); box(details,82,7,5,1,glow)
  for _,x in ipairs({3,118}) do
    module(x,48,7,20)
    box(details,x+2,51,3,12,dark)
    box(details,x+2,52,2,9,glow)
    box(details,x+2,52,2,2,bright)
    box(details,x+1,63,4,1,C(73,94,112))
  end
  -- Small inlaid conductors stop short of the content window.
  box(details,12,73,2,20,C(124,150,169))
  box(details,13,74,1,16,glow)
  box(details,114,73,2,20,C(124,150,169))
  box(details,114,74,1,16,glow)
  box(details,42,25,18,1,dark); box(details,67,25,18,1,dark)
  box(details,43,26,16,1,glow); box(details,68,26,16,1,glow)
elseif rarity=='Epic' then
  -- Reinforced corner exoskeleton with separate vertical power cells.
  module(46,4,36,7)
  box(details,50,7,7,2,glow); box(details,60,7,7,2,glow); box(details,70,7,7,2,glow)
  for _,x in ipairs({2,117}) do
    module(x,40,9,35)
    box(details,x+2,44,5,25,C(52,64,85))
    box(details,x+3,46,3,8,glow); box(details,x+3,57,3,8,glow)
    box(details,x+3,46,2,2,bright); box(details,x+3,57,2,2,bright)
    box(details,x+1,70,7,2,C(104,121,143))
  end
  for _,x in ipairs({4,117}) do
    module(x,122,7,21)
    for yy=126,137,4 do box(details,x+2,yy,3,1,dark) end
  end
  -- Machined braces overlap the rim without intruding into the content area.
  for i=0,5 do
    box(details,10+i,28-i,3,2,C(136,156,178))
    box(details,115-i,28-i,3,2,C(136,156,178))
    box(details,11+i,28-i,1,1,C(243,249,255))
    box(details,115-i,28-i,1,1,C(243,249,255))
  end
  box(details,13,96,1,14,dark); box(details,114,96,1,14,dark)
  for i=0,2 do box(details,56+i*6,25,3,2,glow) end
else
  -- Legendary: raised central crest, stepped shoulders and a lower clasp.
  -- The mass stays titanium; only the inlays use warm metal.
  module(52,3,24,8)
  for y=0,5 do
    local half=math.min(y,5-y)
    box(details,63-half,4+y,half*2+1,1,y<3 and bright or glow)
  end
  box(details,33,8,14,2,dark); box(details,81,8,14,2,dark)
  box(details,34,8,12,1,glow); box(details,82,8,12,1,glow)
  for _,x in ipairs({2,116}) do
    module(x,31,9,23)
    box(details,x+2,35,5,13,dark)
    box(details,x+3,36,3,10,glow)
    box(details,x+3,36,2,2,bright)
    box(details,x+1,50,7,1,C(93,115,136))
  end
  -- Thin gold pins in the metal rim, plus a divided shoulder lip.
  box(details,13,58,1,31,dark); box(details,14,58,1,30,glow)
  box(details,113,58,1,30,glow); box(details,114,58,1,31,dark)
  for _,x in ipairs({4,116}) do
    module(x,139,8,18)
    box(details,x+2,143,4,7,dark)
    box(details,x+2,143,3,1,bright)
    box(details,x+2,144,3,5,glow)
  end
  module(47,166,34,8)
  box(details,52,168,24,3,C(77,94,108))
  box(details,55,168,18,1,glow)
  box(details,61,169,6,2,bright)
  box(details,34,25,20,1,glow); box(details,74,25,20,1,glow)
  for i=0,3 do box(details,56+i*4,25,2,2,dark) end
end
-- Clip machining to the plate, maintaining a fully transparent opening.
for y=0,H-1 do for x=0,W-1 do
  if not silhouette(x,y) or opening(x,y) then details:drawPixel(x,y,0) end
end end
local sprite=Sprite(W,H,ColorMode.RGB)
local function layer(name,im,first)
  local ly=first and sprite.layers[1] or sprite:newLayer(); ly.name=name
  sprite:newCel(ly,1,im,Point(0,0))
end
layer('Shadow',shadow,true); layer('Interior - empty',background)
layer('Titanium plate',plate); layer('Machining and fasteners',details)
sprite:saveAs(out..'/'..rarity..'_Plate.aseprite')
local flat=Image(W,H,ColorMode.RGB); flat:drawSprite(sprite,1)
flat:saveAs(out..'/'..rarity..'_Plate.png')
local frame=Image(W,H,ColorMode.RGB); frame:drawImage(plate); frame:drawImage(details)
frame:saveAs(out..'/'..rarity..'_Frame.png')
background:saveAs(out..'/'..rarity..'_Background.png')
shadow:saveAs(out..'/'..rarity..'_Shadow.png')
local big=Image(W*4,H*4,ColorMode.RGB)
for y=0,H*4-1 do for x=0,W*4-1 do
  local p=flat:getPixel(math.floor(x/4),math.floor(y/4)); local a=app.pixelColor.rgbaA(p)/255
  local function blend(v,b) return math.floor(v*a+b*(1-a)+0.5) end
  big:drawPixel(x,y,C(blend(app.pixelColor.rgbaR(p),20),blend(app.pixelColor.rgbaG(p),28),blend(app.pixelColor.rgbaB(p),40)))
end end
big:saveAs(preview..'/'..rarity..'_Plate_Preview.png')
