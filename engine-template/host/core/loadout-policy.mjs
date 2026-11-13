const group=(id,label,items)=>({id,label,items:items.map(([id,label])=>({id,label}))});
export const weaponGroups=[
 group('assault','突击步枪',[
 ['U_AK12','AK-12'],['U_SCAR-H','SCAR-H'],['U_M416','M416'],['U_SAR21','SAR-21'],['U_AEK971','AEK-971'],['U_FAMAS','FAMAS'],['U_SteyrAug','AUG A3'],['U_M16A4','M16A4'],['U_CZ805','CZ-805'],['U_QBZ951','QBZ-95-1'],['U_GalilACE23','ACE 23'],['U_F2000','F2000'],['U_L85A2','L85A2'],['U_AR160','AR160'],['U_Bulldog','BULLDOG'],['U_AN94','AN-94']]),
 group('carbine','卡宾枪',[
 ['U_AK5C','AK 5C'],['U_ACR','ACW-R'],['U_SG553LB','SG553'],['U_AKU12','AKU-12'],['U_A91','A-91'],['U_GalilACE52','ACE 52 CQB'],['U_G36C','G36C'],['U_M4A1','M4'],['U_GalilACE','ACE 21 CQB'],['U_Type95B','TYPE-95B-1'],['U_MTAR21','MTAR-21'],['U_Groza-1','GROZA-1']]),
 group('pdw','冲锋枪 / PDW',[
 ['U_MX4','MX4'],['U_PP2000','PP-2000'],['U_UMP45','UMP-45'],['U_CBJ-MS','CBJ-MS'],['U_MagpulPDR','PDW-R'],['U_Scorpion','CZ-3A1'],['U_JS2','JS2'],['U_P90','P90'],['U_UMP9','UMP-9'],['U_ASVal','AS VAL'],['U_MP7','MP7'],['U_SR2','SR-2'],['U_MPX','MPX'],['U_Groza-4','GROZA-4']]),
 group('lmg','轻机枪',[
 ['U_Ultimax','U-100 MK5'],['U_Type88','TYPE 88 LMG'],['U_LSAT','LSAT'],['U_Pecheneg','PKP PECHENEG'],['U_QBB95','QBB-95-1'],['U_M240','M240B'],['U_MG4','MG4'],['U_M249','M249'],['U_RPK12','RPK-12'],['U_M60E4','M60-E4'],['U_RPK-74','RPK'],['U_AWS','AWS'],['U_L86A1','L86A2']]),
 group('shotgun','霰弹枪（含副武器及下挂）',[
 ['U_QBS09','QBS-09'],['U_870','870 MCS'],['U_M1014','M1014'],['U_HAWK','HAWK 12G'],['U_SAIGA_20K','SAIGA 12K'],['U_SPAS12','SPAS-12'],['U_UTAS','UTS 15'],['U_DBV12','DBV-12'],['U_DAO12','DAO-12'],['U_SerbuShorty','SHORTY 12G'],['U_M320_SHG','M320 BUCK'],['U_XM25_Flechette','XM25 DART'],['U_M26Mass','M26 MASS'],['U_M26Mass_Flechette','M26 DART'],['U_M26Mass_Slug','M26 SLUG'],['U_M26Mass_Frag','M26 FRAG']]),
 group('dmr','精确射手步枪',[
 ['U_RFB','RFB'],['U_MK11','MK11 MOD 0'],['U_SKS','SKS'],['U_SVD12','SVD-12'],['U_QBU88','QBU-88'],['U_M39EBR','M39 EMR'],['U_GalilACE53','ACE 53 SV'],['U_SCAR-HSV','SCAR-H SV']]),
 group('sniper','狙击步枪',[
 ['U_CS-LR4','CS-LR4'],['U_M40A5','M40A5'],['U_Scout','SCOUT ELITE'],['U_SV98','SV-98'],['U_JNG90','JNG-90'],['U_SRS','338-RECON'],['U_M98B','M98B'],['U_M200','SRR-61'],['U_FY-JS','FY-JS'],['U_GOL','GOL MAGNUM'],['U_L96A1','L115'],['U_SR338','SR338'],['U_CS5','CS5']]),
 group('sidearm','手枪',[
 ['U_P226','P226'],['U_M9','M9'],['U_QSZ92','QSZ-92'],['U_MP443','MP443'],['U_Glock18','G18'],['U_FN57','FN57'],['U_M1911','M1911'],['U_M93R','93R'],['U_CZ75','CZ-75'],['U_Taurus44','.44 MAGNUM'],['U_HK45C','COMPACT 45'],['U_MP412Rex','MP412 REX'],['U_SW40','SW40'],['U_Unica6','UNICA 6'],['U_DesertEagle','DEAGLE 44'],['U_SaddlegunSnp',"MARE'S LEG"]]),
 group('rockets','爆炸物：火箭筒 / 导弹',[
 ['U_RPG7','RPG-7V2'],['U_NLAW','MBT LAW'],['U_SMAW','SMAW'],['U_FGM148','FGM-148 JAVELIN'],['U_SRAW','FGM-172 SRAW'],['U_FIM92','FIM-92 STINGER'],['U_Sa18IGLA','SA-18 IGLA']]),
 group('explosives','爆炸物：榴弹 / 炸药 / 地雷',[
 ['U_M320_HE','M320 HE'],['U_M320_3GL','M320 3GL'],['U_M320_LVG','M320 LVG'],['U_XM25','XM25 AIRBURST'],['U_M26Mass_Frag','M26 FRAG'],['U_M224','M224 MORTAR'],['U_C4','C4（侦察兵）'],['U_C4_Support','C4（支援兵）'],['U_M15','M15 AT MINE'],['U_SLAM','M2 SLAM'],['U_Claymore','M18 CLAYMORE（支援兵）'],['U_Claymore_Recon','M18 CLAYMORE（侦察兵）'],['U_UCAV','UCAV'],['U_AAMine','AA MINE']]),
 group('utility','战术榴弹',[['U_M320_FLASH','M320 FLASH（闪光弹）'],['U_M320_SMK','M320 SMK（烟雾弹）']]),
 group('grenades','手雷 / 投掷物',[
 ['U_M67','M67 FRAG'],['U_V40','V40 MINI'],['U_Grenade_RGO','RGO IMPACT'],['U_M34','M34 INCENDIARY'],['U_M18','M18 SMOKE'],['U_Flashbang','M84 FLASHBANG'],['U_Handflare','HAND FLARE'],['U_DecoyGrenade','DECOY']]),
 group('other','其他主武器',[['dlSHTR','PHANTOM（幻影弓）']])
];
const fromGroups=ids=>weaponGroups.filter(g=>ids.includes(g.id)).flatMap(g=>g.items.map(i=>i.id));
export const weaponPresets=[
 {id:'precision',label:'禁半自动、霰弹枪、狙击步枪',description:'半自动指精确射手步枪；包含霰弹副武器与下挂装备。',weaponIds:[...new Set(fromGroups(['dmr','shotgun','sniper']))].sort()},
 {id:'explosives',label:'禁爆裂物',description:'禁火箭筒、榴弹、炸药、地雷及伤害型投掷物；保留烟雾弹、闪光弹、照明棒和诱饵。',weaponIds:[...new Set([...fromGroups(['rockets','explosives']),'U_M67','U_V40','U_Grenade_RGO','U_M34'])].sort()},
 {id:'unarmed',label:'空手人机',description:'清空 Bot 的武器与装备，并关闭人机载具。适合练枪，真人装备不受影响。',unarmed:true,weaponIds:[...new Set(weaponGroups.flatMap(g=>g.items.map(i=>i.id)))].sort()}
];
const weapons=new Set(weaponGroups.flatMap(g=>g.items.map(i=>i.id)));
export function normalizeLoadout(value){
 if(!value||typeof value!=='object'||Array.isArray(value))throw Error('配置格式无效');
 const checked=(key,known)=>{if(!Array.isArray(value[key])||value[key].length>known.size||value[key].some(x=>typeof x!=='string'||!known.has(x)))throw Error('未知的装备标识：'+key);return [...new Set(value[key])].sort();};
 const p={version:1,weaponIds:checked('weaponIds',weapons)};
 if(value.unarmed!==undefined&&typeof value.unarmed!=='boolean')throw Error('空手人机开关格式错误');
 if(value.unarmed===true)return {...p,weaponIds:[...weapons].sort(),unarmed:true};
 const blocked=new Set(p.weaponIds), allowed=id=>weaponGroups.find(g=>g.id===id).items.filter((i,n)=>id!=='shotgun'||n<9).some(i=>!blocked.has(i.id));
 for(const [type,label]of [['assault','突击兵'],['pdw','工程兵'],['lmg','支援兵'],['sniper','侦察兵']]){
  if(![type,'carbine','dmr','shotgun'].some(allowed))throw Error(label+'没有可选主武器，请至少保留该兵种武器、卡宾枪、精确射手步枪或主武器霰弹枪中的一种');
 }
 return p;
}
