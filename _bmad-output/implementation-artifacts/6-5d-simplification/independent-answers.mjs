import fs from 'node:fs';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
const capRows=[...fs.readFileSync(process.argv[3],'utf8').matchAll(/^\| ((?:record|control|public)\/[^ |]+) \| ([0-9]+) \|$/gm)];
const caps=Object.fromEntries(capRows.map(m=>[m[1],Number(m[2])]));assert.equal(Object.keys(caps).length,capRows.length,'duplicate normative cap');
const input=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const hash=x=>crypto.createHash('sha256').update(x).digest('hex');
const size=n=>{const b=Buffer.alloc(4);b.writeUInt32BE(n);return b;};
const atom=([type,value])=>{
    if(type.startsWith('O:'))return value===null?Buffer.from([0]):Buffer.concat([Buffer.from([1]),atom([type.slice(2),value])]);
    if(type==='U'||type==='B'){const b=type==='U'?Buffer.from(value,'utf8'):Buffer.from(value,'hex');return Buffer.concat([size(b.length),b]);}
    if(type==='B32'){const b=Buffer.from(value,'hex');assert.equal(b.length,32);return b;}
    if(type==='N'||type==='Q'){const b=Buffer.alloc(8);type==='N'?b.writeBigUInt64BE(BigInt(value)):b.writeBigInt64BE(BigInt(value));return b;}
    if(type==='I'){const b=Buffer.alloc(4);b.writeInt32BE(Number(value));return b;}
    throw Error(type);
};
const canonical=x=>{
    if(x&&typeof x==='object'&&Object.keys(x).length===1&&typeof x._integer==='string')return x._integer;
    if(Array.isArray(x))return '['+x.map(canonical).join(',')+']';
    if(x&&typeof x==='object')return '{'+Object.keys(x).sort().map(k=>JSON.stringify(k)+':'+canonical(x[k])).join(',')+'}';
    return JSON.stringify(x);
};
let records=0,keys=0,controls=0;
for(const [name,row] of Object.entries(input.records)){
    assert.equal(row.maxBytes,caps['record/'+name],name+' normative cap');
    let b;
    if(row.fields){
        const count=Buffer.alloc(2);count.writeUInt16BE(row.fields.length);
        b=Buffer.concat([Buffer.from(row.domain+'\0','ascii'),Buffer.from([1]),count,...row.fields.map((f,i)=>Buffer.concat([Buffer.from([i+1]),atom(f)]))]);
    }else b=Buffer.from(canonical(row.json),'utf8');
    assert.equal(b.toString('hex'),row.hex,name+' bytes');assert.equal(b.length,row.length,name+' length');assert.equal(hash(b),row.sha256,name+' hash');assert.ok(b.length<=row.maxBytes,name+' cap');records++;
}
for(const [name,row] of Object.entries(input.keys)){
    const b=Buffer.concat([Buffer.from(row.domain+'\0','ascii'),Buffer.from([1]),...row.fields.map(atom)]);
    assert.equal(b.toString('hex'),row.materialHex,name+' framing');assert.equal(row.prefix+hash(b),row.value,name+' address');keys++;
}
for(const [name,row] of Object.entries(input.controls)){
    assert.equal(row.maxBytes,caps['control/'+name],name+' normative cap');
    const data=row.json??row.value??row.object;
    assert.ok(data,name+' control value missing');
    const b=Buffer.from(canonical(data),'utf8');assert.equal(b.toString('hex'),row.hex,name+' control bytes');assert.equal(b.length,row.length,name+' control length');assert.equal(hash(b),row.sha256,name+' control hash');controls++;
}
for(const [name,row] of Object.entries(input.public)){
    const b=Buffer.from(canonical(row.json),'utf8');assert.equal(row.maxBytes,caps['public/'+name],name+' normative cap');assert.equal(b.toString('hex'),row.hex,name+' public bytes');assert.equal(b.length,row.length);assert.equal(hash(b),row.sha256);
}
assert.equal(Object.keys(caps).length,records+controls+Object.keys(input.public).length,'normative family set');
console.log(`independent Node construction: ${records} wire/JSON answers, ${keys} addressed keys, ${controls} internal controls`);
