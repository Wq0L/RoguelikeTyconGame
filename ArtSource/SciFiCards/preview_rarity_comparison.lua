local root=assert(app.params.root)
local sheet=Image(1024,376,ColorMode.RGB)
for y=0,375 do for x=0,1023 do sheet:drawPixel(x,y,app.pixelColor.rgba(20,28,40,255)) end end
for i,rarity in ipairs({'Common','Rare','Epic','Legendary'}) do
 local s=app.open(root..'/Assets/Art/UI/SciFiCards/'..rarity..'Plate/'..rarity..'_Plate.png')
 local flat=Image(128,180,ColorMode.RGB); flat:drawSprite(s,1)
 for y=0,359 do for x=0,255 do
  local p=flat:getPixel(math.floor(x/2),math.floor(y/2))
  if app.pixelColor.rgbaA(p)>0 then sheet:drawPixel((i-1)*256+x,y+8,p) end
 end end
 s:close()
end
sheet:saveAs(root..'/ArtSource/SciFiCards/Rarity_Comparison.png')
app.exit()
