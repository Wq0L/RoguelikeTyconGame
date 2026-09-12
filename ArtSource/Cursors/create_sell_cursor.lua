local out=assert(app.params.output)
local source=assert(app.params.source)
local function C(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local sprite=Sprite(32,32,ColorMode.RGB)
sprite.layers[1].name='Titanium pointer and sale token'
local im=Image(32,32,ColorMode.RGB)
local function pixel(x,y,c) im:drawPixel(x,y,c) end
local ink=C(15,26,38)
-- Familiar pointer silhouette: the active pixel is (2,2).
local rows={
'X','XX','XLX','XLLX','XLLLX','XLLLLX','XLLLLLX','XLLLLLLX',
'XLLLLLLLX','XLLLLLLLLX','XLLLLLLLLLX','XLLLLLLLLLLX',
'XLLLLLXXXXXXX','XLLXLLX','XLXXLLX','XX XLLX','   XLLX','    XX'}
for y,row in ipairs(rows) do for x=1,#row do
 local ch=row:sub(x,x)
 if ch=='X' then pixel(x+1,y+1,ink)
 elseif ch=='L' then pixel(x+1,y+1,x<=3 and C(246,252,255) or C(185,213,229)) end
end end
-- A gold currency token, readable without text at cursor scale.
for y=16,29 do for x=16,29 do
 local dx,dy=x-22.5,y-22.5
 local r=dx*dx+dy*dy
 if r<=48 then pixel(x,y,ink)
 if r<=35 then pixel(x,y,y<21 and C(255,222,124) or C(211,150,55)) end
 end
end end
local dollar={'  X  ',' XXXX','X X  ',' XXX ','  X X','XXXX ','  X  '}
for y,row in ipairs(dollar) do for x=1,#row do
 if row:sub(x,x)=='X' then pixel(x+19,y+18,C(65,48,28)) end
end end
sprite:newCel(sprite.layers[1],1,im,Point(0,0))
sprite:saveAs(source..'/Sell_Cursor.aseprite')
im:saveAs(out..'/Sell_Cursor.png')
local preview=Image(192,192,ColorMode.RGB)
for y=0,191 do for x=0,191 do
 local c=im:getPixel(math.floor(x/6),math.floor(y/6))
 if app.pixelColor.rgbaA(c)==0 then c=C(66,79,94) end
 preview:drawPixel(x,y,c)
end end
preview:saveAs(source..'/Sell_Cursor_Preview.png')
app.exit()
