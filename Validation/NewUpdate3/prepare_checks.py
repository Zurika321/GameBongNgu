from pathlib import Path

def method(file,signature):
    source=Path(file).read_text(encoding='utf-8-sig');start=source.index(signature);brace=source.index('{',start);depth=1;i=brace+1
    while depth:
        if source[i]=='{':depth+=1
        elif source[i]=='}':depth-=1
        i+=1
    return source[start:i]

animation=method('Assets/Scripts/Animation/CharacterAnimation.cs','    private void Update()')
dropfile='Assets/Scripts/AI/ZoneEnemySpawner.cs'
dropmethods='\n'.join(method(dropfile,s) for s in ['    private bool HasRewardItem(', '    private void SpawnDrops(', '    private sealed class SpawnedEnemy'])
dropmethods+='\n'+next(line for line in Path(dropfile).read_text(encoding='utf-8-sig').splitlines() if 'private int GetExperienceReward()' in line)
prefix='using System;\nusing System.Collections.Generic;\nusing UnityEngine;\n'
Path('Validation/NewUpdate3/ProductionMethods.cs').write_text(prefix+'public partial class AnimationHarness {\n'+animation+'\n}\npublic partial class LootHarness {\n'+dropmethods+'\n}\n',encoding='utf-8')
print('Extracted current production Update/loot methods for API-double checks.')
