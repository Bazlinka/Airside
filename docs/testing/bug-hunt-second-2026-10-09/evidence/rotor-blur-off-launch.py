exec(open('/Users/baileyfleming/Code/Airside/work/bug-hunt-second-15/run.py').read().split('kind=sys.argv[1]')[0])
d=Path('/Users/baileyfleming/Code/Airside/work/bug-hunt-second-15/rotor-blur-off');d.mkdir(parents=True,exist_ok=True)
steps=[]
def add(id,action,value='',capture=False,settle=1):steps.append(dict(id=id,action=action,value=value,capture=capture,settleSeconds=settle))
add('day','time','13:30');add('clear','weather','Clear')
add('planner','planner','KGC',True,2);add('book','book','KGC',False,15)
add('exterior','view','Exterior',True,15)
for i in range(1,6):add('flight-'+str(i),'snapshot','',True,15)
add('overview','overview','',False,1);add('menu','menu','open',True,1)
(d/'plan.json').write_text(json.dumps(dict(protocol=1,expectedCommit=sha,aircraftType='B412',steps=steps),indent=2))
cmd=['/usr/bin/open','-W','-n','-a',str(root/'work/builds/Airside-QA.app'),'--args','-airsideSoak','-airsideSoakMinutes','4','-airsideAgentGameplay',str(d/'plan.json'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',str(d/'player.log'),'-airsideReviewCockpit','-airsideReviewCockpitType','B789','-airsideReviewJourney','KGC','-airsideReviewJourneyRate','40','-airsideSoakAddType','B412','-airsideGraphicsOff','propblur']
try: print("rotor-blur-off finished",g.launch(cmd,d,160)[1])
except Exception as e:print(type(e).__name__,str(e))
