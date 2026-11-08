from pathlib import Path
s=Path('Assets/Scripts/Debug/AdminEdit.cs').read_text(encoding='utf-8-sig')
def method(signature):
    start=s.index(signature);a=s.index('{',start);depth=1;b=a+1
    while depth:
        if s[b]=='{':depth+=1
        elif s[b]=='}':depth-=1
        b+=1
    return s[start:b]
methods='\n'.join(method('    private '+signature) for signature in ['void OpenMenu()', 'void CloseMenu()', 'void BlockUIInput()', 'void AcquireFreeze()', 'void ReleaseFreeze()', 'static bool TryParseFloat(', 'string EditText(', 'int IntRow(', 'float FloatRow(', 'bool VecComponent('])
methods+='\n'+next(line for line in s.splitlines() if 'private static string FormatFloat(' in line)
Path('Validation/AdminEdit/ProductionMenu.cs').write_text('using System;\nusing System.Globalization;\nusing UnityEngine;\nusing UnityEngine.UI;\nusing UnityEditor;\npublic partial class AdminHarness {\n'+methods+'\n}\n',encoding='utf-8')
