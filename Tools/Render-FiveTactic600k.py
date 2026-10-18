"""Insert verified completed-stage data into the task's interactive report."""
import json
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p9-tactics-20261014/MNG5_V2-20261014-r002'
FRAGMENT=Path('C:/Users/USER/.codex/visualizations/2026/10/13/01a1361a-2bf1-77f1-b8d7-d934c752439c/p9-tactics-600k.html')


def main():
    report=json.loads((BASE/'report-data.json').read_text(encoding='utf-8'))
    summary_keys=('games','wins','draws','losses','goalsFor','goalsAgainst','actionCounts','actionRatios','candidateMetrics')
    stage_keys=('step','games','wins','draws','losses','goalsFor','goalsAgainst','actionCounts','actionRatios','metrics')
    compact=dict(run=report['run'],state=report['state'],
        stages=[{key:s[key] for key in stage_keys} for s in report['stages']],
        rows=[dict(step=r['step'],opponent=r['opponent'],summary={key:r['summary'][key] for key in summary_keys}) for r in report['rows']])
    text=FRAGMENT.read_text(encoding='utf-8')
    payload=json.dumps(compact,ensure_ascii=False,separators=(',',':')).replace('<','\\u003c')
    pattern=r'(<script type="application/json" id="p9-tactics-600k-data">)[\s\S]*?(</script>)'
    text,count=re.subn(pattern,lambda m:m[1]+payload+m[2],text)
    assert count==1 and len(text.encode('utf-8'))<1000000
    FRAGMENT.write_text(text,encoding='utf-8')
    print(json.dumps(dict(stages=[s['step'] for s in compact['stages']],bytes=len(text.encode('utf-8')),path=str(FRAGMENT))))


if __name__=='__main__':main()
