"""One-time source migration of the approved exhibition presentation, preserving its original."""
from pathlib import Path
import json

ROOT=Path(__file__).resolve().parent.parent
old=ROOT/'Assets/_Soccer/Manager/Exhibition'
new=ROOT/'Assets/_Soccer/Manager5v5/Demo'
(new/'UI').mkdir(parents=True,exist_ok=True)
(new/'Data').mkdir(parents=True,exist_ok=True)
camera=(old/'Runtime/ExhibitionCamera.cs').read_text(encoding='utf-8-sig')
camera=camera.replace('MachineLearning.Soccer.Manager.Exhibition','MachineLearning.Soccer.Manager5v5.Demo').replace('ExhibitionCamera','FiveDemoCamera').replace('MNG_HumanInput','FiveDemoInput').replace('MNG_MatchController','FiveMatch').replace('MNG_MatchState','FiveMatchState')
camera=camera.replace('match.GetPlayerAvatar(i==2?Team.Navy:Team.Red,i==3?3:0).transform','match.players[i==2?5:i==3?3:0].transform')
camera=camera.replace('void Select(View view,bool blend)','public void Select(View view,bool blend)')
(new/'Runtime/FiveDemoCamera.cs').write_text(camera,encoding='utf-8')
views=(old/'Runtime/ExhibitionApp.Views.cs').read_text(encoding='utf-8-sig')
home=views[views.index('        void DrawHome()'):views.index('        void LanguagePopup()')]
home=home.replace('ShowImage("guide",0)','ShowGuide(0)').replace('ShowImage("credits",0)','ShowCredits()')
app=(old/'Runtime/ExhibitionApp.cs').read_text(encoding='utf-8-sig')
helpers=app[app.index('        VisualElement Box('):app.rindex('    }')]
chevron=views[views.index('        void Chevron('):views.rindex('    }')]
head='using System;\nusing UnityEngine;\nusing UnityEngine.UIElements;\nnamespace MachineLearning.Soccer.Manager5v5.Demo\n{\n    public sealed partial class FiveDemoApp\n    {\n'
(new/'Runtime/FiveDemoApp.Home.cs').write_text(head+home+helpers+chevron+'    }\n}\n',encoding='utf-8')
strings=json.loads((old/'Data/strings.json').read_text(encoding='utf-8-sig'))
strings['entries']=[e for e in strings['entries'] if e['key'].startswith('MNG.Exhibition.') and not any(w in e['key'] for w in ['command','stepTab','referenceTab','stepsNotice','referenceNotice'])]
overrides={
 'tagline':('강화학습 기반 5대5 축구 게임','A 5v5 football game with reinforcement learning'),
 'redCaption':('H 키로 RED 공격수 1의 조작에 개입합니다.','Press H to take control of RED attacker 1.'),
 'humanMode':('인간 조작 개입','Human intervention'),
 'simMode':('관전 모드','Spectator mode'),
 'neural':('강화학습 감독','Reinforcement learning manager'),
 'rule':('규칙형 감독','Rule manager')}
for key,(ko,en) in overrides.items():
    full='MNG.Exhibition.'+key
    strings['entries']=[e for e in strings['entries'] if e['key']!=full]
    strings['entries'].append(dict(key=full,ko=ko,en=en))
(new/'Data/strings.json').write_text(json.dumps(strings,ensure_ascii=False,indent=2),encoding='utf-8')
css=(old/'UI/Exhibition.uss').read_text(encoding='utf-8-sig')
css+='\n.viewport { flex-grow: 1; overflow: hidden; }\n.stage { position: absolute; width: 1920px; height: 1080px; transform-origin: 0 0; overflow: hidden; }\n'
(new/'UI/FiveDemo.uss').write_text(css,encoding='utf-8')
(new/'UI/FiveDemo.uxml').write_text('<ui:UXML xmlns:ui="UnityEngine.UIElements">\n <ui:VisualElement name="viewport" class="viewport" picking-mode="Ignore">\n  <ui:VisualElement name="stage" class="stage" picking-mode="Ignore" />\n </ui:VisualElement>\n</ui:UXML>\n',encoding='utf-8')
print('Copied original home/camera design and localized only the new demo data.')
