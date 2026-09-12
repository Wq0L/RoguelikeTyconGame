-- Run with Aseprite --batch --script create_popup.lua
local root = app.params['output']
assert(root, 'Provide --script-param output=<directory>')
local W, H = 160, 112
local sprite = Sprite(W, H, ColorMode.RGB)
local function rgba(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local function inside(x,y,l,t,r,b,radius)
  if x<l or x>r or y<t or y>b then return false end
  local dx=math.min(x-l,r-x)
  local dy=math.min(y-t,b-y)
  local cuts = {[0]={},[1]={1},[2]={2,1},[3]={3,1,1},[4]={4,2,1,1},[5]={5,3,2,1,1}}
  local rows=cuts[radius]
  return dy>=radius or dx>=rows[dy+1]
end
local layers={}
local function layer(name,fn)
  local target
  if #layers==0 then target=sprite.layers[1] else target=sprite:newLayer() end
  target.name=name
  local img=Image(W,H,ColorMode.RGB)
  for y=0,H-1 do for x=0,W-1 do
    local c=fn(x,y)
    if c then img:drawPixel(x,y,c) end
  end end
  sprite:newCel(target,1,img,Point(0,0))
  layers[#layers+1]=target
  return img
end
layer('Shadow',function(x,y)
  if inside(x,y,2,5,157,110,5) and not inside(x,y,2,2,157,107,5) then
    if y==110 or x==2 or x==157 then return rgba(20,23,33,24) end
    return rgba(20,23,33,48)
  end
end)
layer('Background',function(x,y)
  if not inside(x,y,7,7,152,102,2) then return end
  if y==7 then return rgba(171,177,187) end
  if x==7 or y==8 then return rgba(193,199,207) end
  if x==152 then return rgba(220,225,231) end
  if y==102 then return rgba(238,241,244) end
  return rgba(224,229,235)
end)
layer('Frame',function(x,y)
  if not inside(x,y,2,2,157,107,5) or inside(x,y,7,7,152,102,2) then return end
  if not inside(x,y,3,3,156,106,4) then return rgba(77,86,102) end
  if not inside(x,y,4,4,155,105,3) then
    if y>H/2 then return rgba(165,176,189) end
    return rgba(255,255,255)
  end
  if inside(x,y,6,6,153,103,3) then
    if y==6 or x==6 then return rgba(145,157,173) end
    return rgba(249,251,253)
  end
  if y>=104 then return rgba(189,200,212) end
  if x>=154 then return rgba(215,224,232) end
  return rgba(239,245,249)
end)
local slice=sprite:newSlice(Rectangle(0,0,W,H))
slice.name='Popup_9Slice'
slice.center=Rectangle(12,12,W-24,H-26)
sprite:saveAs(root..'/Popup.aseprite')
local composite=Image(W,H,ColorMode.RGB)
composite:drawSprite(sprite,1)
composite:saveAs(root..'/Popup_Combined.png')
for _,target in ipairs(layers) do
  for _,other in ipairs(layers) do other.isVisible=other==target end
  local img=Image(W,H,ColorMode.RGB)
  img:drawSprite(sprite,1)
  img:saveAs(root..'/Popup_'..target.name..'.png')
end
for _,target in ipairs(layers) do target.isVisible=true end
local preview=Image(W*4,H*4,ColorMode.RGB)
for y=0,H*4-1 do for x=0,W*4-1 do
  local c=composite:getPixel(math.floor(x/4),math.floor(y/4))
  local a=app.pixelColor.rgbaA(c)/255
  local bg= ((math.floor(x/32)+math.floor(y/32))%2==0) and 43 or 47
  local function blend(v) return math.floor(v*a+bg*(1-a)+0.5) end
  preview:drawPixel(x,y,rgba(blend(app.pixelColor.rgbaR(c)),blend(app.pixelColor.rgbaG(c)),blend(app.pixelColor.rgbaB(c))))
end end
preview:saveAs(root..'/Popup_Preview_4x.png')
print('Popup exported: 3 layers, 160x112, 9-slice 12/14/12/12.')
