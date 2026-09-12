-- Original 32px player-stat glyphs, authored and exported in Aseprite.
local out=assert(app.params.output)
local preview=assert(app.params.preview)
local function C(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local P={steel=C(173,196,212),light=C(235,248,255),shade=C(95,123,148),deep=C(39,59,80),cyan=C(97,219,238),cyanDark=C(49,137,169),orange=C(245,166,111),pink=C(228,138,186),purple=C(175,145,234),gold=C(238,197,110),goldDark=C(157,112,61)}
local function rect(im,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and xx<im.width and yy>=0 and yy<im.height then im:drawPixel(xx,yy,c) end end end
end
local function line(im,x0,y0,x1,y1,c,width)
  local dx,dy=math.abs(x1-x0),-math.abs(y1-y0)
  local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1
  local e=dx+dy
  while true do
    rect(im,x0,y0,width or 1,width or 1,c)
    if x0==x1 and y0==y1 then break end
    local e2=2*e
    if e2>=dy then e=e+dy; x0=x0+sx end
    if e2<=dx then e=e+dx; y0=y0+sy end
  end
end
local function poly(im,points,col)
  for y=0,31 do for x=0,31 do
    local yes=false; local j=#points
    for i=1,#points do
      local a,b=points[i],points[j]
      if (a[2]>y+0.5)~=(b[2]>y+0.5) and x+0.5<(b[1]-a[1])*(y+0.5-a[2])/(b[2]-a[2])+a[1] then yes=not yes end
      j=i
    end
    if yes then im:drawPixel(x,y,col) end
  end end
end
local function disk(im,cx,cy,rx,ry,col,inner)
  for y=0,31 do for x=0,31 do
    local d=((x-cx)/rx)^2+((y-cy)/ry)^2
    if d<=1 and (not inner or ((x-cx)/(rx-inner))^2+((y-cy)/(ry-inner))^2>=1) then im:drawPixel(x,y,col) end
  end end
end
local function spark(im,x,y,c)
  rect(im,x,y-3,1,7,c); rect(im,x-3,y,7,1,c)
  rect(im,x-1,y-1,3,3,c)
end
local specs={
  {'HarvestDamage','HARVEST DAMAGE',P.orange},
  {'AreaRadius','AREA RADIUS',P.cyan},
  {'AttackSpeed','ATTACK SPEED',P.cyan},
  {'CritChance','CRIT CHANCE',P.purple},
  {'CritMultiplier','CRIT MULTIPLIER',P.pink},
  {'HarvestScoreMultiplier','HARVEST SCORE',P.gold}
}
local flats={}
for index,spec in ipairs(specs) do
  local im=Image(32,32,ColorMode.RGB)
  local hi=Image(32,32,ColorMode.RGB)
  if index==1 then
    -- One bold blade. No loose sparks, panels or secondary device silhouette.
    poly(im,{{5,25},{9,29},{16,22},{12,18}},P.shade)
    poly(im,{{9,18},{12,15},{20,23},{17,26}},P.steel)
    line(hi,9,18,12,15,P.light)
    poly(im,{{13,16},{24,4},{29,3},{28,10},{19,21}},P.steel)
    poly(hi,{{14,16},{25,5},{28,4},{19,18}},P.light)
    line(hi,19,19,27,10,P.orange,2)
    line(hi,6,25,9,28,P.cyan)
  elseif index==2 then
    -- Expansion in four directions, visually distinct from the critical sight.
    rect(im,13,13,6,6,P.cyan)
    poly(im,{{12,8},{16,3},{20,8},{18,8},{18,11},{14,11},{14,8}},P.steel)
    poly(im,{{12,24},{14,24},{14,21},{18,21},{18,24},{20,24},{16,29}},P.steel)
    poly(im,{{8,12},{8,14},{11,14},{11,18},{8,18},{8,20},{3,16}},P.steel)
    poly(im,{{24,12},{29,16},{24,20},{24,18},{21,18},{21,14},{24,14}},P.steel)
    rect(hi,14,13,4,1,P.ice or P.light)
    line(hi,5,15,7,13,P.light); line(hi,14,5,16,3,P.light)
  elseif index==3 then
    -- A large single lightning bolt with two short speed trails.
    poly(im,{{18,3},{27,3},{20,13},{27,13},{12,29},{15,19},{8,19}},P.cyan)
    poly(hi,{{18,3},{24,3},{16,15},{12,15}},P.light)
    line(hi,15,27,24,16,P.cyanDark)
    rect(im,3,10,7,2,P.steel); rect(im,2,15,5,2,P.steel)
  elseif index==4 then
    -- One sight and a bold center. No additional rings or glints.
    disk(im,16,16,10,10,P.steel,3)
    disk(im,16,16,3,3,P.purple)
    rect(im,15,2,3,9,P.steel); rect(im,15,22,3,8,P.steel)
    rect(im,2,15,9,3,P.steel); rect(im,22,15,8,3,P.steel)
    line(hi,9,8,12,6,P.light); rect(hi,15,2,3,1,P.light)
    rect(hi,15,14,2,2,P.light)
  elseif index==5 then
    -- Familiar multiplier shorthand; the actual value remains dynamic UI text.
    line(im,3,13,10,20,P.steel,3); line(im,3,20,10,13,P.steel,3)
    poly(im,{{17,7},{26,7},{29,10},{29,15},{20,22},{29,22},{29,26},{15,26},{15,20},{24,13},{24,11},{19,11},{19,14},{15,14},{15,10}},P.pink)
    rect(hi,18,7,8,1,P.light); rect(hi,16,24,12,1,P.light)
  else
    -- Broad trophy silhouette; no engraved symbol at icon scale.
    rect(im,7,5,18,3,P.steel)
    poly(im,{{9,8},{23,8},{22,16},{19,20},{13,20},{10,16}},P.gold)
    poly(im,{{18,8},{23,8},{22,16},{19,20},{16,20}},P.goldDark)
    rect(im,14,20,4,5,P.steel)
    rect(im,10,25,12,2,P.shade); rect(im,8,27,16,2,P.steel)
    line(im,7,9,4,9,P.gold,2); line(im,4,9,4,14,P.gold,2); line(im,4,14,10,18,P.gold,2)
    line(im,24,9,27,9,P.goldDark,2); line(im,27,9,27,14,P.goldDark,2); line(im,27,14,22,18,P.goldDark,2)
    rect(hi,8,5,16,1,P.light); rect(hi,11,9,1,5,P.light)
    rect(hi,9,27,14,1,P.light)
  end
  local merged=Image(32,32,ColorMode.RGB); merged:drawImage(im); merged:drawImage(hi)
  local outline=Image(32,32,ColorMode.RGB)
  for y=0,31 do for x=0,31 do
    if app.pixelColor.rgbaA(merged:getPixel(x,y))==0 then
      local adjacent=false
      for yy=math.max(0,y-1),math.min(31,y+1) do for xx=math.max(0,x-1),math.min(31,x+1) do
        if app.pixelColor.rgbaA(merged:getPixel(xx,yy))>0 then adjacent=true end
      end end
      if adjacent then outline:drawPixel(x,y,C(16,28,43)) end
    end
  end end
  local sp=Sprite(32,32,ColorMode.RGB)
  for n,entry in ipairs({{'Outline',outline},{'Metal and accent',im},{'Highlights',hi}}) do
    local ly=n==1 and sp.layers[1] or sp:newLayer(); ly.name=entry[1]
    sp:newCel(ly,1,entry[2],Point(0,0))
  end
  sp:saveAs(out..'/Stat_'..spec[1]..'.aseprite')
  local flat=Image(32,32,ColorMode.RGB); flat:drawSprite(sp,1)
  flat:saveAs(out..'/Stat_'..spec[1]..'.png'); flats[index]=flat
end
-- Labeled proof sheet. Labels and backgrounds never enter exported icons.
local font={A={'010','101','111','101','101'},C={'011','100','100','100','011'},D={'110','101','101','101','110'},E={'111','100','110','100','111'},G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},V={'101','101','101','101','010'},Y={'101','101','010','010','010'},[' ']={'000','000','000','000','000'}}
local function label(im,str,x,y,col)
  for ch in str:gmatch('.') do local g=font[ch]; if g then
    for yy=1,5 do for xx=1,3 do if g[yy]:sub(xx,xx)=='1' then im:drawPixel(x+xx-1,y+yy-1,col) end end end
  end; x=x+4 end
end
local sheet=Image(288,162,ColorMode.RGB); sheet:clear(C(20,28,40))
label(sheet,'PLAYER STATS',8,7,P.light)
for i,spec in ipairs(specs) do
  local x=6+((i-1)%3)*94; local y=22+math.floor((i-1)/3)*68
  rect(sheet,x,y,88,61,C(29,42,57))
  rect(sheet,x,y,88,1,C(74,94,115))
  sheet:drawImage(flats[i],Point(x+28,y+7))
  label(sheet,spec[2],x+math.floor((88-(#spec[2]*4-1))/2),y+49,P.steel)
  rect(sheet,x+40,y+58,8,1,spec[3])
end
local big=Image(864,486,ColorMode.RGB)
for y=0,485 do for x=0,863 do big:drawPixel(x,y,sheet:getPixel(math.floor(x/3),math.floor(y/3))) end end
big:saveAs(preview..'/Player_Simple_Preview.png')

