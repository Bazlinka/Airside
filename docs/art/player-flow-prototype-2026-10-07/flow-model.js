/* Local design fixtures only. Not the Unity simulation or its tuning data. */
(function (root) {
  'use strict';
  const copy = value => JSON.parse(JSON.stringify(value));
  const TYPES = {
    SF34: {name:'Saab 340B', seats:34, range:1000, price:60000, check:320, tier:0, level:0, resale:42000},
    DH8D: {name:'Dash 8 Q400', seats:78, range:2000, price:120000, check:900, tier:1, level:1, resale:84000},
    A320: {name:'Airbus A320', seats:180, range:6000, price:260000, check:1800, tier:2, level:2, resale:182000}
  };
  const ROUTES = [
    {code:'KGC',name:'Kingscote',from:'ADL',km:125,seats:25,cost:337,return:525,tier:0,x:36,y:71},
    {code:'PLO',name:'Port Lincoln',from:'ADL',km:245,seats:55,cost:620,return:980,tier:1,x:29,y:57},
    {code:'BHQ',name:'Broken Hill',from:'ADL',km:425,seats:28,cost:750,return:1060,tier:0,x:57,y:34},
    {code:'MEL',name:'Melbourne',from:'ADL',km:640,seats:140,cost:1450,return:2390,tier:2,x:70,y:78},
    {code:'SYD',name:'Sydney',from:'MEL',km:710,seats:65,cost:1100,return:1950,tier:2,x:85,y:51},
    {code:'SIN',name:'Singapore',from:'ADL',km:5400,seats:160,cost:6700,return:9200,tier:3,x:10,y:10},
    {code:'BNE',name:'Brisbane',from:'SYD',km:750,seats:65,cost:1120,return:1900,tier:2,x:88,y:28},
    {code:'PER',name:'Perth',from:'BNE',km:3600,seats:140,cost:3100,return:4300,tier:2,x:14,y:65},
    {code:'ADL',name:'Adelaide',from:'PER',km:2100,seats:120,cost:2300,return:3290,tier:2,x:45,y:63}
  ];
  const CONTRACTS = [
    {id:'KGC',title:'Connect Kangaroo Island',route:'KGC',goal:3,bonus:600,perFlight:90,tier:0,role:'Passenger',deadline:'Three eligible rotations; no sample deadline'},
    {id:'PLO',title:'Spencer Gulf service',route:'PLO',goal:4,bonus:1200,perFlight:120,tier:1,role:'Passenger',deadline:'Four eligible rotations; no sample deadline'},
    {id:'FRT',title:'Regional freight link',route:'BHQ',goal:3,bonus:800,perFlight:100,tier:1,role:'Freighter',deadline:'Three eligible rotations; no sample deadline'}
  ];
  const tierName = n => ['Provisional','Regional','Domestic','International'][n];
  const clock = n => `${String(Math.floor(n/60)%24).padStart(2,'0')}:${String(n%60).padStart(2,'0')}`;
  const plane = (id,type='SF34',base='ADL',extra={}) => ({id,type,base,role:'Passenger',wear:8,stand:base==='ADL'?'R1':'Remote base',booking:null,job:null,repeat:null,flights:0,...extra});
  function create(scenario='starter',name='Soak Air',code='SA',colour='#2c6080',coach=true) {
    let s={schema:1,scenario,name,code,colour,coach,funds:2253,reliability:100,tier:0,level:0,time:13*60+50,flights:0,manual:0,selected:'VH-PAX',fleet:[plane('VH-PAX')],bases:[{code:'ADL',name:'Adelaide',capacity:2}],contract:null,contractHistory:[],history:[],events:[],goal:'first',nextId:2,settings:{dark:false,large:false,sound:true,motion:false},issue:null,shedOwner:null};
    if(scenario!=='starter') {
      Object.assign(s,{funds:480000,tier:2,level:2,flights:48,manual:15,goal:'network',coach:false});
      s.bases=[{code:'ADL',name:'Adelaide',capacity:6},{code:'MEL',name:'Melbourne',capacity:8}];
      s.fleet=[plane('VH-PAX'),plane('VH-CRB','DH8D','ADL',{stand:'R2',wear:42}),plane('VH-O01','DH8D','MEL',{flights:11})];
    }
    if(scenario==='recovery') {
      s.funds=2100;s.issue='ground';s.fleet[0].booking={route:'KGC',departure:s.time+20,stage:0,cost:337,expected:525};
      s.fleet[1].repeat={route:'PLO',interval:4,paused:true,reason:'Not enough funds for the next dispatch.'};s.fleet[1].wear=89;
    }
    return s;
  }
  const aircraft = (s,id=s.selected) => s.fleet.find(a=>a.id===id);
  const route = code => ROUTES.find(r=>r.code===code);
  const fail = reason => ({ok:false,reason});
  const ok = message => ({ok:true,message});
  const event = (s,text) => {s.events.unshift({at:clock(s.time),text});s.events=s.events.slice(0,30);};
  const capacity = (s,base) => s.bases.find(b=>b.code===base)?.capacity || 0;
  function availability(a) {return a.job?'In maintenance':a.booking?(a.booking.ferry?'Ferry to Adelaide':['Fuelling','Boarding','Ready to depart','Taxiing','En route','Returning','Unloading'][a.booking.stage]):'Available';}
  function routeReason(s,a,r) {
    if(!a)return 'Choose an owned aircraft.';
    if(!r)return 'Choose a destination.';
    if(r.from!==a.base)return `This route starts at ${r.from}; ${a.id} is based at ${a.base}.`;
    if(s.tier<r.tier)return `${tierName(r.tier)} capability required.`;
    if(TYPES[a.type].range<r.km)return `Beyond ${TYPES[a.type].name}'s sample range of ${TYPES[a.type].range.toLocaleString()} km.`;
    return '';
  }
  function forecast(a,r) {
    const seats=Math.min(r.seats,TYPES[a.type].seats);
    const cost=Math.round(r.cost*(a.type==='A320'?1.45:a.type==='DH8D'?1.1:1));
    const revenue=Math.round(r.return*seats/r.seats);
    return {seats,cost,revenue,margin:revenue-cost};
  }
  function scheduleReason(s,a,r,departure) {
    const reason=routeReason(s,a,r);if(reason)return reason;
    if(a.booking)return 'This aircraft already has a committed rotation. Review that booking first.';
    if(a.job)return 'This aircraft is unavailable until its maintenance journey has returned to stand.';
    if(a.wear>=85)return 'A maintenance check is required before dispatch.';
    if(s.funds<forecast(a,r).cost)return `Dispatch needs $${forecast(a,r).cost}; available funds are $${s.funds}.`;
    if(!Number.isFinite(departure)||departure<s.time+10)return 'Allow at least 10 sample minutes to prepare.';
    if(departure>=24*60)return 'Choose a departure before midnight in this single-day study.';
    return '';
  }
  function schedule(s,id,code,departure) {
    const a=aircraft(s,id),r=route(code),reason=scheduleReason(s,a,r,departure);if(reason)return fail(reason);
    const f=forecast(a,r);s.funds-=f.cost;a.booking={route:code,departure,stage:0,cost:f.cost,expected:f.revenue};s.manual++;s.selected=id;
    event(s,`${id}: rotation to ${r.name} booked for ${clock(departure)}; $${f.cost} dispatch charged.`);return ok('Rotation booked. Preparation is now automatic.');
  }
  function settle(s,a,away=false) {
    const b=a.booking;if(!b)return fail('No committed service to finish.');
    if(b.ferry){a.base='ADL';a.stand=b.stand||freeStand(s,a.type,a.id);a.booking=null;event(s,`${a.id} arrived at Adelaide. Ferry earned no revenue or flight credit.`);return ok('Ferry arrived. Ready at Adelaide.');}
    let bonus=0;let complete=false;
    if(s.contract&&s.contract.id!=='done') {
      const def=CONTRACTS.find(c=>c.id===s.contract.id);
      if(def.route===b.route&&def.role===a.role&&a.base==='ADL') {
        s.contract.progress++;bonus=def.perFlight;
        if(s.contract.progress>=def.goal){bonus+=def.bonus;complete=true;s.contractHistory.unshift({...s.contract,title:def.title});s.contract=null;}
      }
    }
    s.funds+=b.expected+bonus;a.flights++;s.flights++;a.wear=Math.min(100,a.wear+3);
    const result={id:`${a.id}-${a.flights}`,aircraft:a.id,route:b.route,cost:b.cost,revenue:b.expected,bonus,net:b.expected+bonus-b.cost,at:clock(s.time),away};
    s.history.unshift(result);s.history=s.history.slice(0,30);a.booking=null;
    if(s.flights>=3&&s.tier===0){s.tier=1;event(s,'Regional capability earned in the sample career.');}
    if(s.coach)s.coach=false;
    event(s,`${a.id} returned. Net $${result.net}${complete?' · contract completed':''}.`);return ok(`Flight complete. Net $${result.net}${complete?' including contract completion reward':''}.`);
  }
  const PHASES=['Preparing','Starting / pushback','Taxiing to shed','Apron shutdown','Tug positioning','Under repair','Tug exit','Returning to stand'];
  function advance(s,id=s.selected,away=false) {
    const a=aircraft(s,id);if(!a)return fail('Select an aircraft.');
    if(a.job) {
      if(s.issue==='return'&&a.job.stage===7)return fail('No compatible return stand is free. The aircraft waits safely.');
      s.time+=5;
      if(a.job.stage===5){a.wear=0;a.job.repaired=true;event(s,`${a.id}: repair work finished; return journey still required.`);}
      if(a.job.stage===7){a.job=null;s.shedOwner=null;event(s,`${a.id} parked and available after maintenance.`);return ok('Maintenance journey complete. Aircraft is available.');}
      a.job.stage++;return ok(PHASES[a.job.stage]);
    }
    if(a.booking) {
      if(s.issue==='ground'&&a.booking.stage===0)return fail('Waiting for simulated aircraft EM204 at the taxi holding point. Tower handles clearance.');
      s.time+=5;
      if(away||a.booking.ferry||a.booking.stage===6)return settle(s,a,away);
      a.booking.stage++;return ok(availability(a));
    }
    if(a.repeat&&!a.repeat.paused&&!away) {
      const r=route(a.repeat.route);const result=schedule(s,a.id,r.code,s.time+15);
      if(!result.ok){a.repeat.paused=true;a.repeat.reason=result.reason;event(s,`${a.id}: repeat paused. ${result.reason}`);}
      return result;
    }
    return fail('No active movement. Plan a rotation or request a check.');
  }
  function cancelReason(a){if(!a?.booking)return 'No booking to cancel.';if(a.booking.ferry)return 'An accepted ferry cannot be cancelled in this study.';if(a.booking.stage>=3)return 'Departure is already underway; cancellation is unavailable.';return '';}
  function cancel(s,id){const a=aircraft(s,id),reason=cancelReason(a);if(reason)return fail(reason);a.booking=null;s.reliability=Math.max(0,s.reliability-1);event(s,`${id}: booking cancelled; dispatch charge retained and reliability −1 point (sample policy).`);return ok('Booking cancelled. Repeat plan, if any, remains separate.');}
  function checkReason(s,a) {
    if(!a)return 'Choose an aircraft.';if(a.booking)return 'Finish or cancel the committed flight first.';if(a.job)return 'A check is already in progress.';
    if(s.funds<TYPES[a.type].check)return `Check costs $${TYPES[a.type].check}; available $${s.funds}.`;
    if(a.base==='ADL'&&s.shedOwner&&s.shedOwner!==a.id)return 'The local shed is occupied. Wait for the existing job to return.';return '';
  }
  function startCheck(s,id){const a=aircraft(s,id),reason=checkReason(s,a);if(reason)return fail(reason);s.funds-=TYPES[a.type].check;a.job={stage:0,timed:a.base!=='ADL'||a.type==='A320',repaired:false};if(!a.job.timed)s.shedOwner=id;event(s,`${id}: check accepted; $${TYPES[a.type].check} charged once.`);return ok(a.job.timed?'Timed outsourced check started.':'Maintenance journey started. No passenger loading.');}
  function advanceTimed(s,id){const a=aircraft(s,id);if(!a?.job?.timed)return advance(s,id);a.wear=0;a.job=null;s.time+=5;event(s,`${id}: timed outsourced check completed.`);return ok('Check finished. Aircraft available.');}
  function freeStand(s,type,except=null){const stands=type==='A320'?['G1','G2']:['R1','R2','R3','R4'];return stands.find(x=>!s.fleet.some(a=>a.id!==except&&(a.base==='ADL'&&a.stand===x||a.booking?.ferry&&a.booking.stand===x)))||null;}
  function buyReason(s,type,base){const t=TYPES[type];if(!t)return 'Choose a supported offer.';if(!s.bases.some(b=>b.code===base))return 'Open this base first.';if(s.fleet.length>=25)return 'The airline has reached its fleet limit.';if(s.tier<t.tier)return `${tierName(t.tier)} capability required.`;if(base==='ADL'&&s.level<t.level)return 'Upgrade Adelaide operating capability first.';if(!ROUTES.some(r=>r.from===base&&r.km<=t.range&&r.tier<=s.tier))return 'No sample route from this base fits this aircraft.';if(s.fleet.filter(a=>a.base===base).length>=capacity(s,base))return 'No base capacity available. Expand or choose another base.';if(base==='ADL'&&!freeStand(s,type))return 'No compatible sample stand is free.';if(s.funds<t.price)return `Purchase costs $${t.price.toLocaleString()}; available $${s.funds.toLocaleString()}.`;return '';}
  function buy(s,type,base){const reason=buyReason(s,type,base);if(reason)return fail(reason);const id=`VH-${base==='ADL'?'P':'O'}${String(s.nextId++).padStart(2,'0')}`;s.funds-=TYPES[type].price;s.fleet.push(plane(id,type,base,{stand:base==='ADL'?freeStand(s,type):'Remote base'}));s.selected=id;event(s,`${id} bought at ${base}.`);return ok(`${id} is ready at ${base}.`);}
  function sellReason(a){if(!a)return 'Aircraft no longer in fleet.';if(a.booking)return 'Cancel or finish its booked flight before selling.';if(a.job)return 'Finish its check and return before selling.';return '';}
  function sell(s,id){const a=aircraft(s,id),reason=sellReason(a);if(reason)return fail(reason);s.funds+=TYPES[a.type].resale;s.fleet=s.fleet.filter(x=>x.id!==id);if(s.selected===id)s.selected=s.fleet[0]?.id||null;event(s,`${id} sold for $${TYPES[a.type].resale}; repeat plan removed.`);return ok('Aircraft sold. Roster and funds updated.');}
  function ferryReason(s,a){if(!a||a.base==='ADL')return 'Only outstation-to-Adelaide relocation is supported.';if(a.booking||a.job)return 'Aircraft must be available before relocation.';if(s.fleet.filter(x=>x.base==='ADL'||x.booking?.ferry).length>=capacity(s,'ADL'))return 'Adelaide capacity is full, including inbound ferries.';if(s.level<TYPES[a.type].level)return 'Adelaide capability does not support this type.';if(!freeStand(s,a.type))return 'No compatible Adelaide stand is free.';if(s.funds<900)return 'Ferry dispatch needs $900.';return '';}
  function ferry(s,id){const a=aircraft(s,id),reason=ferryReason(s,a);if(reason)return fail(reason);s.funds-=900;a.booking={ferry:true,route:'ADL',stage:4,cost:900,expected:0,departure:s.time,stand:freeStand(s,a.type)};event(s,`${id}: ferry to Adelaide accepted for $900.`);return ok('Ferry inbound; no commercial settlement will be awarded.');}
  function contractReason(s,c){if(s.contract)return 'Finish or abandon your active contract first.';if(s.tier<c.tier)return `${tierName(c.tier)} capability required.`;if(!s.fleet.some(a=>a.base==='ADL'&&a.role===c.role&&!routeReason(s,a,route(c.route))))return `No eligible ${c.role.toLowerCase()} aircraft at Adelaide.`;return '';}
  function accept(s,id){const c=CONTRACTS.find(x=>x.id===id);if(!c)return fail('Offer not found.');const reason=contractReason(s,c);if(reason)return fail(reason);s.contract={id,progress:0};event(s,`${c.title} accepted.`);return ok('Contract accepted. Plan an eligible rotation.');}
  function repeat(s,id,code,interval){const a=aircraft(s,id);if(s.manual<12)return fail(`Unlock after 12 manually planned services; sample count ${s.manual}.`);const reason=routeReason(s,a,route(code));if(reason)return fail(reason);if(![2,4,8].includes(interval))return fail('Choose a supported sample interval.');a.repeat={route:code,interval,paused:false,reason:''};event(s,`${id}: repeat to ${code} every ${interval} h enabled while open.`);return ok('Repeat plan enabled. Existing booking unchanged.');}
  function action(s,kind,args={}) {
    const a=aircraft(s,args.id);
    if(kind==='schedule')return schedule(s,args.id,args.route,args.departure);
    if(kind==='cancel')return cancel(s,args.id);
    if(kind==='check')return startCheck(s,args.id);
    if(kind==='advance')return a?.job?.timed?advanceTimed(s,args.id):advance(s,args.id);
    if(kind==='buy')return buy(s,args.type,args.base);
    if(kind==='sell')return sell(s,args.id);
    if(kind==='ferry')return ferry(s,args.id);
    if(kind==='accept')return accept(s,args.contract);
    if(kind==='repeat')return repeat(s,args.id,args.route,args.interval);
    if(kind==='abandon'){if(!s.contract)return fail('No active contract.');s.contract=null;s.reliability=Math.max(0,s.reliability-2);event(s,'Contract abandoned; reliability −2 points (sample policy).');return ok('Contract abandoned.');}
    if(kind==='stand'){if(!a||a.base!=='ADL')return fail('Stand assignment is available only at Adelaide.');if(a.booking||a.job)return fail('Assign a stand while the aircraft is available.');const allowed=a.type==='A320'?['G1','G2']:['R1','R2','R3','R4'];if(!allowed.includes(args.stand))return fail('Stand is incompatible with this type.');if(s.fleet.some(x=>x.id!==a.id&&x.stand===args.stand&&x.base==='ADL'))return fail('This stand is reserved by another aircraft.');a.stand=args.stand;event(s,`${a.id}: stand assignment ${args.stand}.`);return ok('Assignment updated. Physical movement is not simulated by this study.');}
    if(kind==='refit'){if(!a||a.type!=='SF34')return fail('This sample conversion is supported only for the Saab.');if(a.booking||a.job)return fail('Finish active work before changing role.');a.role=a.role==='Passenger'?'Freighter':'Passenger';event(s,`${a.id}: role changed to ${a.role}.`);return ok(`Role changed to ${a.role}; route and view eligibility updated.`);}
    if(kind==='base'){if(s.tier<2)return fail('Domestic capability required to open an outstation.');if(!['MEL','SYD','BNE','PER'].includes(args.base))return fail('Choose a supported outstation.');if(s.bases.some(b=>b.code===args.base))return fail('This base is already open.');if(s.bases.length>=4)return fail('Three outstations are the current limit.');if(s.funds<25000)return fail('Base opening needs $25,000.');s.funds-=25000;s.bases.push({code:args.base,name:{MEL:'Melbourne',SYD:'Sydney',BNE:'Brisbane',PER:'Perth'}[args.base],capacity:8});event(s,`${args.base} base opened.`);return ok('Base opened. Select it when comparing aircraft offers.');}
    if(kind==='upgrade'){if(s.level>=2)return fail('Adelaide capability is already at the sample maximum.');if(s.funds<18000)return fail('Upgrade needs $18,000.');s.funds-=18000;s.level++;s.bases[0].capacity=s.level===1?4:6;event(s,'Adelaide base upgraded.');return ok('Capability and capacity upgraded.');}
    if(kind==='repeat-pause'){if(!a?.repeat)return fail('No repeat plan.');a.repeat.paused=!a.repeat.paused;a.repeat.reason='';event(s,`${a.id}: repeat ${a.repeat.paused?'paused':'resumed'}; committed booking unchanged.`);return ok('Repeat setting updated.');}
    if(kind==='repeat-remove'){if(!a?.repeat)return fail('No repeat plan.');a.repeat=null;event(s,`${a.id}: repeat removed; committed booking unchanged.`);return ok('Repeat plan removed.');}
    return fail('Unsupported study command.');
  }
  function validSave(s){
    const finite=n=>Number.isFinite(n)&&n>=0, text=x=>typeof x==='string'&&x.length<200, bases=['ADL','MEL','SYD','BNE','PER'];
    if(s?.schema!==1||!text(s.name)||!s.name.trim()||!(/^[A-Z]{2,3}$/).test(s.code)||!(/^#[a-f0-9]{6}$/i).test(s.colour)||!finite(s.funds)||!finite(s.time)||!finite(s.manual)||!finite(s.flights)||!Number.isInteger(s.nextId)||s.nextId<1||!Number.isInteger(s.tier)||s.tier<0||s.tier>3||!Number.isInteger(s.level)||s.level<0||s.level>2||!finite(s.reliability)||s.reliability>100||!Array.isArray(s.bases)||s.bases.length>4||!Array.isArray(s.fleet)||s.fleet.length>25||!Array.isArray(s.history)||!Array.isArray(s.events)||!Array.isArray(s.contractHistory)||!s.settings)return false;
    if(!s.bases.some(b=>b.code==='ADL')||!s.bases.every(b=>bases.includes(b.code)&&text(b.name)&&Number.isInteger(b.capacity)&&b.capacity>0&&b.capacity<=8)||new Set(s.bases.map(b=>b.code)).size!==s.bases.length)return false;
    if(!s.fleet.every(a=>TYPES[a.type]&&text(a.id)&&s.bases.some(b=>b.code===a.base)&&['Passenger','Freighter'].includes(a.role)&&finite(a.wear)&&a.wear<=100&&finite(a.flights)&&text(a.stand)&&(!a.booking||(route(a.booking.route)&&finite(a.booking.departure)&&finite(a.booking.cost)&&finite(a.booking.expected)&&Number.isInteger(a.booking.stage)&&a.booking.stage>=0&&a.booking.stage<=6))&&(!a.job||(Number.isInteger(a.job.stage)&&a.job.stage>=0&&a.job.stage<=7&&typeof a.job.timed==='boolean'))&&(!a.repeat||(route(a.repeat.route)&&[2,4,8].includes(a.repeat.interval)&&typeof a.repeat.paused==='boolean'&&text(a.repeat.reason)))))return false;
    if(new Set(s.fleet.map(a=>a.id)).size!==s.fleet.length||s.selected!==null&&!s.fleet.some(a=>a.id===s.selected))return false;
    if(s.contract&&(!CONTRACTS.some(c=>c.id===s.contract.id)||!Number.isInteger(s.contract.progress)||s.contract.progress<0))return false;
    if(!s.history.every(h=>text(h.id)&&text(h.aircraft)&&text(h.route)&&text(h.at)&&finite(h.cost)&&finite(h.revenue)&&finite(h.bonus)&&Number.isFinite(h.net))||!s.events.every(e=>text(e.text)&&text(e.at))||!s.contractHistory.every(c=>text(c.title)&&text(c.id)&&finite(c.progress)))return false;
    return ['dark','large','sound','motion'].every(k=>typeof s.settings[k]==='boolean');
  }
  root.FlowModel={create,copy,TYPES,ROUTES,CONTRACTS,tierName,clock,aircraft,route,availability,forecast,routeReason,scheduleReason,checkReason,buyReason,sellReason,ferryReason,contractReason,cancelReason,action,advance,settle,PHASES,capacity,validSave,event};
  if(typeof module!=='undefined')module.exports=root.FlowModel;
})(typeof window!=='undefined'?window:globalThis);
