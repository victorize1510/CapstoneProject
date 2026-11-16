"""Generate narrow URP 17.3 camera-occlusion shader variants from the installed package.

Source shaders remain untouched. Unity Companion License applies to derived files.
Run from repository root, only outside Play Mode. Deliberately fails on package drift.
"""
from pathlib import Path
import re
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = next((ROOT / 'Capstone/Library/PackageCache').glob('com.unity.render-pipelines.universal@*'))
SOURCE = PACKAGE / 'Shaders'
DEST = ROOT / 'Capstone/Assets/Game/WorldFeedback/Resources/WorldFeedback'
INCLUDES = DEST / 'CameraPasses'

def generate():
    INCLUDES.mkdir(parents=True, exist_ok=True)
    copied = {}
    def save(name, content, original=None):
        (INCLUDES / name).write_text('// Derived from Unity URP 17.3.0; see ../LICENSE-URP.md.\n' + content, encoding='utf8')
        if original:
            copied[name] = hashlib.sha256(original.encode()).hexdigest()

    for name in ('LitInput.hlsl', 'SimpleLitInput.hlsl', 'UnlitInput.hlsl'):
        raw = (SOURCE / name).read_text(encoding='utf8')
        assert raw.count('CBUFFER_END') == 1, name
        content = raw.replace('CBUFFER_END', '    float _CapstoneCameraFade;\n    float _CapstoneActorFade;\nCBUFFER_END')
        content += '\n#include "../CameraOcclusionClip.hlsl"\n'
        save(name, content, raw)

    passes = ('LitForwardPass.hlsl', 'LitGBufferPass.hlsl', 'SimpleLitForwardPass.hlsl',
              'SimpleLitGBufferPass.hlsl', 'UnlitForwardPass.hlsl', 'UnlitGBufferPass.hlsl',
              'DepthOnlyPass.hlsl', 'LitDepthNormalsPass.hlsl', 'SimpleLitDepthNormalsPass.hlsl',
              'UnlitDepthNormalsPass.hlsl')
    for name in passes:
        raw = (SOURCE / name).read_text(encoding='utf8')
        token = 'UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);'
        assert raw.count(token) == 1, name
        save(name, raw.replace(token, token + '\n    CapstoneCameraClip(input.positionCS);'), raw)

    for family in ('Lit', 'SimpleLit', 'Unlit'):
        raw = (SOURCE / (family + '.shader')).read_text(encoding='utf8')
        name = 'CameraDither' + family
        content = re.sub(r'Shader "[^"]+"', f'Shader "Capstone/World/{name}"', raw, count=1)
        content = content.replace('Properties\n    {', 'Properties\n    {\n        [HideInInspector] _CapstoneCameraFade("Camera Occlusion", Range(0,1)) = 0\n        [HideInInspector] _CapstoneActorFade("Near Player", Range(0,1)) = 0')
        for inc in ('LitInput.hlsl', 'SimpleLitInput.hlsl', 'UnlitInput.hlsl') + passes:
            content = content.replace('Packages/com.unity.render-pipelines.universal/Shaders/' + inc, 'CameraPasses/' + inc)
        # Motion vectors deliberately remain solid; this project uses no temporal AA.
        # ShadowCaster/Meta/selection passes also preserve original physical geometry.
        (DEST / (name + '.shader')).write_text('// Derived from Unity URP 17.3.0; see LICENSE-URP.md.\n' + content, encoding='utf8')
        copied[family + '.shader'] = hashlib.sha256(raw.encode()).hexdigest()
    (DEST / 'LICENSE-URP.md').write_text((PACKAGE / 'LICENSE.md').read_text(encoding='utf8') +
        '\nDerived camera shader files: copied from installed URP 17.3.0; only named fade CBUFFER field and matching camera-space clip added. '
        'Original package files are unmodified. Regenerate with Tools/CombatVisuals/generate_camera_adapters.py after a package upgrade.\n', encoding='utf8')
    (DEST / 'CameraPasses/source-manifest.json').write_text(json.dumps({'package': PACKAGE.name, 'sha256': copied}, indent=2), encoding='utf8')
    print('Generated 3 shader adapters and 13 narrow pass/input copies from', PACKAGE.name)

if __name__ == '__main__':
    generate()
