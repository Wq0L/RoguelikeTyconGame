-- Original device silhouettes drawn directly in Aseprite. No external artwork.
local out=assert(app.params.output)
local preview=assert(app.params.preview)
local N=48
local function C(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local P={edge=C(16,25,39),metal=C(176,197,210),light=C(238,249,251),shade=C(99,127,149),deep=C(38,56,77),black=C(23,36,53),cyan=C(88,221,229),ice=C(207,255,248),orange=C(250,163,96),amber=C(255,225,166),purple=C(173,148,243),pink=C(235,130,186),gold=C(235,194,111)}
local function rect(im,x,y,w,h,col)
  for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and xx<N and yy>=0 and yy<N then im:drawPixel(xx,yy,col) end end end
end
local function line(im,x,y,tx,ty,col,w)
  local dx,dy=math.abs(tx-x),-math.abs(ty-y); local sx,sy=x<tx and 1 or -1,y<ty and 1 or -1; local e=dx+dy
  while true do rect(im,x,y,w or 1,w or 1,col); if x==tx and y==ty then break end; local e2=2*e
    if e2>=dy then e=e+dy; x=x+sx end
    if e2<=dx then e=e+dx; y=y+sy end
  end
end
local function poly(im,pts,col,bevel)
  local mask={}
  local function has(x,y) return mask[y*N+x] and x>=0 and x<N and y>=0 and y<N end
  for y=0,N-1 do for x=0,N-1 do
    local yes=false; local j=#pts
    for i=1,#pts do local a,b=pts[i],pts[j]
      if (a[2]>y+.5)~=(b[2]>y+.5) and x+.5<(b[1]-a[1])*(y+.5-a[2])/(b[2]-a[2])+a[1] then yes=not yes end
      j=i
    end
    if yes then mask[y*N+x]=true end
  end end
  for y=0,N-1 do for x=0,N-1 do if has(x,y) then
    local cc=col
    if bevel then
      if not has(x,y-1) or not has(x-1,y) then cc=P.light
      elseif not has(x,y+1) or not has(x+1,y) then cc=P.shade end
    end
    im:drawPixel(x,y,cc)
  end end end
end
local function panel(im,x,y,w,h)
  poly(im,{{x+2,y},{x+w-2,y},{x+w,y+2},{x+w,y+h-2},{x+w-2,y+h},{x+2,y+h},{x,y+h-2},{x,y+2}},P.metal,true)
end
local function ellipse(im,cx,cy,rx,ry,col,thickness,gate)
  for y=0,N-1 do for x=0,N-1 do
    local d=((x-cx)/rx)^2+((y-cy)/ry)^2
    if d<=1 and (not thickness or ((x-cx)/(rx-thickness))^2+((y-cy)/(ry-thickness))^2>=1) and (not gate or gate(x,y)) then im:drawPixel(x,y,col) end
  end end
end
local function screw(im,x,y)
  rect(im,x,y,2,2,P.shade); rect(im,x,y,2,1,P.light)
end
local specs={
 {'HarvestDamage','PLASMA SHEAR','HASAT HASARI',P.orange},
 {'AreaRadius','FIELD PROJECTOR','ALAN YARICAPI',P.cyan},
 {'AttackSpeed','PULSE DRIVE','SALDIRI HIZI',P.cyan},
 {'CritChance','OPTIC ARRAY','KRITIK SANSI',P.purple},
 {'CritMultiplier','OVERDRIVE CORE','KRITIK CARPANI',P.pink},
 {'HarvestScoreMultiplier','YIELD PROCESSOR','HASAT PUANI',P.gold}
}
local flats={}
for idx,spec in ipairs(specs) do
  local body=Image(N,N,ColorMode.RGB); local tech=Image(N,N,ColorMode.RGB); local light=Image(N,N,ColorMode.RGB)
  if idx==1 then
    -- Open industrial C-jaw; the cutting plane is suspended between the jaws.
    poly(body,{{9,30},{23,30},{24,36},{20,44},{12,44},{10,40}},P.shade,true)
    rect(tech,13,35,7,2,P.deep); rect(tech,13,39,6,2,P.deep)
    poly(body,{{8,13},{15,8},{28,8},{30,14},{20,18},{18,27},{28,29},{29,36},{11,36},{6,30},{6,18}},P.metal,true)
    poly(body,{{17,8},{35,5},{41,9},{39,15},{23,17}},P.metal,true)
    poly(body,{{19,27},{36,29},{41,34},{38,39},{21,35}},P.metal,true)
    poly(tech,{{12,15},{19,13},{19,17},{15,20},{15,29},{11,28}},P.deep)
    rect(tech,8,22,3,6,P.shade)
    poly(tech,{{24,11},{36,8},{37,10},{26,13}},P.shade)
    poly(tech,{{25,31},{36,33},{36,35},{25,33}},P.shade)
    rect(tech,35,14,4,3,P.deep); rect(tech,35,27,4,4,P.deep)
    line(light,37,17,37,27,P.orange,2); line(light,37,19,37,25,P.amber)
    rect(light,32,19,1,2,P.orange); rect(light,32,24,2,1,P.orange)
    screw(tech,11,13); screw(tech,11,31); rect(light,20,10,3,1,P.cyan)
  elseif idx==2 then
    -- Isometric field generator with layered, discontinuous projection rings.
    poly(body,{{9,29},{24,22},{40,29},{40,37},{25,44},{9,37}},P.shade,true)
    poly(body,{{9,28},{24,21},{40,28},{25,36}},P.metal,true)
    poly(tech,{{14,29},{24,24},{35,29},{25,33}},P.deep)
    poly(body,{{20,20},{28,20},{29,28},{24,31},{19,28}},P.shade,true)
    ellipse(tech,24,21,4,2,P.deep)
    ellipse(light,24,20,2,1,P.ice)
    line(light,24,12,24,18,P.cyan)
    ellipse(light,24,13,10,4,P.cyan,1,function(x,y) return x<22 or x>25 end)
    ellipse(light,24,8,16,5,P.cyan,1,function(x,y) return (x<21 or x>26) and (y<8 or x<13 or x>35) end)
    line(light,11,9,13,10,P.ice); line(light,30,4,34,5,P.ice)
    rect(tech,13,35,2,3,P.deep); rect(tech,17,37,2,3,P.deep)
    line(light,29,37,35,34,P.cyan); screw(tech,12,28); screw(tech,35,28)
  elseif idx==3 then
    -- A horizontal pulse accelerator: three sequenced cells and a forward throat.
    poly(body,{{10,15},{17,9},{36,9},{42,15},{42,29},{35,35},{15,35},{10,29}},P.metal,true)
    poly(body,{{38,14},{45,17},{45,26},{38,30}},P.shade,true)
    poly(tech,{{16,15},{35,15},{38,18},{38,26},{34,29},{16,29}},P.deep)
    for i=0,2 do
      local x=17+i*6
      panel(body,x,14,4,17)
      rect(tech,x+1,17,2,10,P.shade)
      rect(light,x+1,19,2,6,P.cyan)
      rect(light,x+1,19,1,2,P.ice)
    end
    rect(tech,41,18,2,7,P.deep); rect(light,42,20,1,3,P.ice)
    for _,y in ipairs({16,22,28}) do
      panel(body,6,y,7,3); rect(light,3,y+1,3,1,P.cyan)
    end
    rect(tech,20,11,9,1,P.shade); rect(tech,20,33,9,1,P.shade)
    screw(tech,35,12); screw(tech,35,30)
  elseif idx==4 then
    -- A diagonally mounted optic with a faceted lens, not a target symbol.
    poly(body,{{10,25},{22,30},{19,41},{12,44},{6,39}},P.shade,true)
    poly(tech,{{12,32},{17,34},{15,39},{11,40},{9,37}},P.deep)
    poly(body,{{9,16},{23,4},{34,5},{43,15},{43,25},{30,36},{20,35},{9,25}},P.metal,true)
    poly(tech,{{14,16},{25,8},{33,9},{39,16},{39,23},{28,31},{21,30},{14,23}},P.deep)
    ellipse(body,27,20,10,9,P.shade)
    ellipse(tech,27,19,8,7,P.deep)
    poly(light,{{21,16},{28,12},{33,16},{32,23},{26,26},{21,22}},P.purple)
    poly(light,{{21,16},{28,12},{26,19},{21,22}},C(217,205,255))
    poly(light,{{26,19},{33,16},{32,23},{26,26}},C(118,99,179))
    rect(light,23,15,2,3,P.light)
    panel(body,7,16,6,12); rect(tech,9,19,2,6,P.deep)
    rect(light,9,20,2,2,P.cyan)
    screw(tech,24,6); screw(tech,39,24); rect(tech,22,33,6,1,P.shade)
  elseif idx==5 then
    -- Dual clamp overdrive chamber, with a suspended split-phase core.
    panel(body,11,35,27,8)
    poly(body,{{5,13},{11,8},{17,10},{17,16},{13,20},{14,30},{19,34},{17,38},{9,34},{5,26}},P.metal,true)
    poly(body,{{32,10},{39,8},{43,14},{43,27},{38,35},{32,38},{29,34},{35,28},{35,20},{31,16}},P.metal,true)
    poly(tech,{{8,16},{11,13},{13,15},{10,20},{10,27},{12,30},{10,31},{8,26}},P.deep)
    poly(tech,{{37,15},{39,14},{40,17},{40,26},{37,31},{35,30},{38,25}},P.deep)
    poly(light,{{24,5},{30,14},{29,23},{24,31},{19,23},{18,14}},P.pink)
    poly(light,{{24,7},{24,19},{20,22},{20,14}},C(255,211,235))
    poly(light,{{24,19},{28,15},{27,23},{24,28}},C(158,81,151))
    line(light,15,22,18,20,P.pink); line(light,30,20,33,23,P.pink)
    rect(tech,18,37,14,3,P.deep)
    for x=20,29,4 do rect(light,x,38,2,1,P.pink) end
    screw(tech,12,11); screw(tech,37,11)
    rect(tech,8,22,2,3,P.shade); rect(tech,39,22,2,3,P.shade)
  else
    -- A harvest-yield data cartridge with a layered luminous storage stack.
    for x=13,33,4 do rect(body,x,38,2,7,P.shade); rect(light,x,41,2,3,P.gold) end
    poly(body,{{10,8},{14,4},{32,4},{39,11},{39,37},{35,41},{10,41},{7,37},{7,12}},P.metal,true)
    poly(tech,{{13,10},{29,10},{34,15},{34,32},{29,35},{13,35}},P.deep)
    poly(body,{{30,5},{38,12},{30,12}},P.shade,true)
    for i=0,2 do
      local y=25-i*6
      poly(light,{{16,y},{24,y-3},{31,y},{23,y+4}},i==2 and P.gold or C(176,132,71))
      line(light,16,y,23,y+3,P.amber)
      line(light,24,y-3,29,y-1,P.gold)
    end
    rect(tech,10,15,1,14,P.shade)
    rect(tech,15,6,10,1,P.shade); rect(tech,14,37,13,2,P.deep)
    rect(light,16,38,3,1,P.cyan); rect(light,21,38,3,1,P.cyan)
    screw(tech,10,10); screw(tech,35,35)
  end
  local combined=Image(N,N,ColorMode.RGB); combined:drawImage(body); combined:drawImage(tech); combined:drawImage(light)
  local outline=Image(N,N,ColorMode.RGB)
  for y=0,N-1 do for x=0,N-1 do
    if app.pixelColor.rgbaA(combined:getPixel(x,y))==0 then
      local touch=false
      for yy=math.max(0,y-1),math.min(N-1,y+1) do for xx=math.max(0,x-1),math.min(N-1,x+1) do
        if app.pixelColor.rgbaA(combined:getPixel(xx,yy))>0 then touch=true end
      end end
      if touch then outline:drawPixel(x,y,P.edge) end
    end
  end end
  local sp=Sprite(N,N,ColorMode.RGB)
  for n,item in ipairs({{'Contour',outline},{'Machined metal',body},{'Recesses and hardware',tech},{'Energy and reflections',light}}) do
    local ly=n==1 and sp.layers[1] or sp:newLayer(); ly.name=item[1]; sp:newCel(ly,1,item[2],Point(0,0))
  end
  sp:saveAs(out..'/Stat_'..spec[1]..'.aseprite')
  local flat=Image(N,N,ColorMode.RGB); flat:drawSprite(sp,1); flats[idx]=flat
  flat:saveAs(out..'/Stat_'..spec[1]..'.png')
end
local font={A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},J={'001','001','001','101','010'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},V={'101','101','101','101','010'},Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},[' ']={'000','000','000','000','000'}}
local function label(im,str,x,y,col)
  for ch in str:gmatch('.') do local g=font[ch]; if g then for yy=1,5 do for xx=1,3 do if g[yy]:sub(xx,xx)=='1' then im:drawPixel(x+xx-1,y+yy-1,col) end end end end; x=x+4 end
end
local sheet=Image(318,226,ColorMode.RGB); sheet:clear(C(17,25,38))
label(sheet,'PLAYER SYSTEMS',10,9,P.light)
for i,spec in ipairs(specs) do
  local x=8+((i-1)%3)*104; local y=25+math.floor((i-1)/3)*98
  -- Separate proof-board tiles, not included in the transparent game PNGs.
  for yy=y,y+91 do for xx=x,x+95 do sheet:drawPixel(xx,yy,C(29,42,56)) end end
  sheet:drawImage(flats[i],Point(x+24,y+9))
  label(sheet,spec[2],x+math.floor((96-(#spec[2]*4-1))/2),y+67,P.light)
  label(sheet,spec[3],x+math.floor((96-(#spec[3]*4-1))/2),y+78,spec[4])
end
local big=Image(954,678,ColorMode.RGB)
for y=0,677 do for x=0,953 do big:drawPixel(x,y,sheet:getPixel(math.floor(x/3),math.floor(y/3))) end end
big:saveAs(preview..'/Player_Devices_Preview.png')

