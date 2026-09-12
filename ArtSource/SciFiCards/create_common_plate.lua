-- Original Common card: a restrained, serviceable titanium module.
local out=assert(app.params.output)
local preview=assert(app.params.preview)
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
  return rounded(x,y,6,5,121,173,6)
    or rounded(x,y,3,44,9,67,2)
    or rounded(x,y,3,112,9,135,2)
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
screw(14,13); screw(109,13); screw(14,159); screw(109,159)
-- Two subtle hinge barrels give Common a practical, manufactured silhouette.
for _,y in ipairs({46,114}) do
  box(details,4,y,4,20,C(135,155,172))
  box(details,4,y,1,20,C(217,232,240))
  box(details,5,y+4,3,1,C(70,92,112))
  box(details,5,y+15,3,1,C(70,92,112))
  box(details,8,y+1,1,18,C(89,111,128))
end
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
box(details,91,160,5,3,C(147,207,200))
box(details,91,160,5,1,C(207,245,232))
-- Interrupted milled seam at the shoulders, kept away from the empty interior.
box(details,13,25,16,1,C(139,160,177)); box(details,99,25,16,1,C(139,160,177))
box(details,13,26,15,1,C(243,249,252)); box(details,100,26,15,1,C(243,249,252))
box(details,28,169,25,1,C(135,157,176)); box(details,56,169,5,1,C(135,157,176))
local sprite=Sprite(W,H,ColorMode.RGB)
local function layer(name,im,first)
  local ly=first and sprite.layers[1] or sprite:newLayer(); ly.name=name
  sprite:newCel(ly,1,im,Point(0,0))
end
layer('Shadow',shadow,true); layer('Interior - empty',background)
layer('Titanium plate',plate); layer('Machining and fasteners',details)
sprite:saveAs(out..'/Common_Plate.aseprite')
local flat=Image(W,H,ColorMode.RGB); flat:drawSprite(sprite,1)
flat:saveAs(out..'/Common_Plate.png')
local frame=Image(W,H,ColorMode.RGB); frame:drawImage(plate); frame:drawImage(details)
frame:saveAs(out..'/Common_Frame.png')
background:saveAs(out..'/Common_Background.png')
shadow:saveAs(out..'/Common_Shadow.png')
local big=Image(W*4,H*4,ColorMode.RGB)
for y=0,H*4-1 do for x=0,W*4-1 do
  local p=flat:getPixel(math.floor(x/4),math.floor(y/4)); local a=app.pixelColor.rgbaA(p)/255
  local function blend(v,b) return math.floor(v*a+b*(1-a)+0.5) end
  big:drawPixel(x,y,C(blend(app.pixelColor.rgbaR(p),20),blend(app.pixelColor.rgbaG(p),28),blend(app.pixelColor.rgbaB(p),40)))
end end
big:saveAs(preview..'/Common_Plate_Preview.png')
