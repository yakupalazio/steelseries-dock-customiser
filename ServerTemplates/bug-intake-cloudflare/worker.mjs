// Private, optional Cloudflare Worker template. NOT deployed with the EXE.
// Bind D1 as DB and private R2 as UPLOADS; set GH_TOKEN and HASH_SALT secrets.
const validCategories=new Set(["OLED / Dock","Medya / Şarkı sözleri",
"Pil / Cihaz","Arayüz / Animasyon","Performans","Diğer"]);
const reply=(status,payload)=>new Response(JSON.stringify(payload),{
 status,headers:{"content-type":"application/json;charset=utf-8",
  "cache-control":"no-store","x-content-type-options":"nosniff"}});
const text=(v,min,max)=>typeof v==="string"&&
 v.trim().length>=min&&v.length<=max;
async function rate(request,env){
 const ip=request.headers.get("cf-connecting-ip")||"unknown";
 const raw=await crypto.subtle.digest("SHA-256",
  new TextEncoder().encode(env.HASH_SALT+"#"+ip));
 const key=Array.from(new Uint8Array(raw)).map(x=>x.toString(16)
  .padStart(2,"0")).join("");
 const hour=Math.floor(Date.now()/3_600_000);
 await env.DB.prepare("INSERT INTO bug_rate(digest,hour,count) VALUES(?,?,1) "+
  "ON CONFLICT(digest) DO UPDATE SET count=CASE WHEN hour=excluded.hour "+
  "THEN count+1 ELSE 1 END,hour=excluded.hour").bind(key,hour).run();
 const row=await env.DB.prepare("SELECT count FROM bug_rate WHERE digest=?")
  .bind(key).first();
 return (row?.count??99)<=5;
}
function validate(x){
 return x&&text(x.Title,5,120)&&text(x.Description,10,4000)&&
  validCategories.has(x.Category)&&text(x.AppVersion,1,70)&&
  (x.OperatingSystem==null||text(x.OperatingSystem,1,180))&&
  (x.AttachmentBase64==null||(typeof x.AttachmentBase64==="string"&&
   x.AttachmentBase64.length<=2_700_000&&
   ["screenshot.png","screenshot.jpg","screenshot.jpeg"].includes(
    x.AttachmentFileName)));
}
async function attach(body,env){
 if(!body.AttachmentBase64)return "";
 if(!env.UPLOADS)throw new Error("Private R2 is not configured");
 const text=atob(body.AttachmentBase64);
 if(text.length>2_000_000)throw new Error("Image too large");
 const bytes=Uint8Array.from(text,c=>c.charCodeAt(0));
 const png=bytes[0]===137&&bytes[1]===80&&bytes[2]===78&&bytes[3]===71;
 const jpg=bytes[0]===255&&bytes[1]===216&&bytes[2]===255;
 if(!png&&!jpg)throw new Error("Invalid image");
 if(png!==body.AttachmentFileName.endsWith(".png"))
  throw new Error("Image type mismatch");
 const key="reports/"+crypto.randomUUID()+(png?".png":".jpg");
 await env.UPLOADS.put(key,bytes,{httpMetadata:{
  contentType:png?"image/png":"image/jpeg"}});
 return key; // Private object key only. Never expose a public R2 URL.
}
export default {async fetch(request,env){
 if(new URL(request.url).pathname!=="/api/bugs")return reply(404,{ok:false});
 if(request.method!=="POST")return reply(405,{ok:false});
 if(!env.DB||!env.GH_TOKEN||!env.GH_OWNER||!env.GH_REPO||
  !env.HASH_SALT)return reply(503,{ok:false,error:"not configured"});
 if(!(request.headers.get("content-type")||"").includes("application/json"))
  return reply(415,{ok:false});
 if(Number(request.headers.get("content-length")||0)>2_900_000)
  return reply(413,{ok:false});
 const reader=request.body?.getReader();if(!reader)return reply(400,{ok:false});
 const chunks=[];let total=0;
 while(true){
  const {done,value}=await reader.read();if(done)break;
  total+=value.byteLength;if(total>2_900_000)
   {await reader.cancel();return reply(413,{ok:false});}
  chunks.push(value);
 }
 const bytes=new Uint8Array(total);let position=0;
 for(const chunk of chunks){bytes.set(chunk,position);position+=chunk.length;}
 let body;try{body=JSON.parse(new TextDecoder().decode(bytes));}
 catch{return reply(400,{ok:false});}
 if(!validate(body))return reply(422,{ok:false,error:"invalid report"});
 if(!await rate(request,env))
  return reply(429,{ok:false,error:"rate limit exceeded"});
 let key;try{key=await attach(body,env);}
 catch{return reply(422,{ok:false,error:"image unavailable"});}
 const issueBody=[
  "**Category:** "+body.Category,"**App:** "+body.AppVersion,
  "**Reported:** "+new Date().toISOString(),
  body.OperatingSystem?"**Windows:** "+body.OperatingSystem:"",
  key?"**Private R2 screenshot key:** "+key:"",
  "### Reproduction details\n"+body.Description.trim()
 ].filter(Boolean).join("\n\n");
 let result;
 try{result=await fetch("https://api.github.com/repos/"+
  encodeURIComponent(env.GH_OWNER)+"/"+
  encodeURIComponent(env.GH_REPO)+"/issues",{
   method:"POST",headers:{"Accept":"application/vnd.github+json",
    "Authorization":"Bearer "+env.GH_TOKEN,
    "X-GitHub-Api-Version":"2022-11-28",
    "Content-Type":"application/json",
    "User-Agent":"dock-customiser-report-intake"},
   body:JSON.stringify({title:"[Bug] "+body.Title.trim(),
    body:issueBody,labels:["bug"]})});}
 catch{return reply(503,{ok:false,error:"github unavailable"});}
 if(!result.ok)return reply(503,{ok:false,error:"github rejected report"});
 const issue=await result.json();
 return reply(201,{ok:true,issueNumber:issue.number});
}};
