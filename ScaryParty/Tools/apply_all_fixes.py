import re
import os

def fix_character_prefabs():
    target_files = [
        'ScaryParty/Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab',
        'ScaryParty/Assets/Piaaria/Resources/CharacterVisual.prefab'
    ]

    for path in target_files:
        if not os.path.exists(path):
            continue
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()

        # 1. Update Armature transform (ID 1906586154864152665) -> Identity (0, 0, 0)
        arm_pattern = r'(--- !u!4 &1906586154864152665\nTransform:\n(?:.*\n)*?  m_LocalRotation: )\{[^}]+\}(\n(?:.*\n)*?  m_LocalEulerAnglesHint: )\{[^}]+\}'
        arm_replacement = r'\g<1>{x: 0, y: 0, z: 0, w: 1}\g<2>{x: 0, y: 0, z: 0}'
        content = re.sub(arm_pattern, arm_replacement, content)

        # 2. Update Cylinder.002 transform (ID 1292450855994392747) -> Identity (0, 0, 0)
        cyl_pattern = r'(--- !u!4 &1292450855994392747\nTransform:\n(?:.*\n)*?  m_LocalRotation: )\{[^}]+\}(\n(?:.*\n)*?  m_LocalEulerAnglesHint: )\{[^}]+\}'
        cyl_replacement = r'\g<1>{x: 0, y: 0, z: 0, w: 1}\g<2>{x: 0, y: 0, z: 0}'
        content = re.sub(cyl_pattern, cyl_replacement, content)

        # 3. Update central bone transform (ID 3792014296699959291) -> Y is up (y=0.46774173, z=0)
        cen_pos_pattern = r'(--- !u!4 &3792014296699959291\nTransform:\n(?:.*\n)*?  m_LocalPosition: )\{[^}]+\}'
        content = re.sub(cen_pos_pattern, r'\g<1>{x: 0, y: 0.46774173, z: 0}', content)

        cen_rot_pattern = r'(--- !u!4 &3792014296699959291\nTransform:\n(?:.*\n)*?  m_LocalRotation: )\{[^}]+\}'
        content = re.sub(cen_rot_pattern, r'\g<1>{x: 0, y: 0, z: 0, w: 1}', content)

        cen_euler_pattern = r'(--- !u!4 &3792014296699959291\nTransform:\n(?:.*\n)*?  m_LocalEulerAnglesHint: )\{[^}]+\}'
        content = re.sub(cen_euler_pattern, r'\g<1>{x: 0, y: 0, z: 0}', content)

        # 4. Update AABB of SkinnedMeshRenderer (center y: 0.80, z: 0.0)
        aabb_pattern = r'  m_AABB:\n    m_Center: \{x: [^,]+, y: [^,]+, z: [^}]+\}\n    m_Extent: \{x: [^,]+, y: [^,]+, z: [^}]+\}'
        aabb_replacement = '  m_AABB:\n    m_Center: {x: 0.0, y: 0.80, z: 0.0}\n    m_Extent: {x: 0.46, y: 0.80, z: 0.33}'
        content = re.sub(aabb_pattern, aabb_replacement, content)

        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f'[PREFAB_OK] Character prefab fixed: {path}')

def fix_tables_in_file(file_path):
    if not os.path.exists(file_path):
        return
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    count_mesa = 0
    count_cadeira = 0

    # Replace local Y of mesa comum from -1.1991158 to -1.0221158 (lifts table by ~17.7cm so legs touch floor -1.60m)
    # Match blocks of mesa comum
    old_mesa_y = '-1.1991158'
    new_mesa_y = '-1.0221158'
    if old_mesa_y in content:
        content_new = content.replace(f'y: {old_mesa_y}', f'y: {new_mesa_y}')
        count_mesa = (len(content) - len(content_new)) // len(old_mesa_y)
        content = content_new

    # Adjust cadeira local Y if present (-0.100535154 -> -0.168)
    old_chair_y = '-0.100535154'
    new_chair_y = '-0.168'
    if old_chair_y in content:
        content_new = content.replace(f'y: {old_chair_y}', f'y: {new_chair_y}')
        count_cadeira = (len(content) - len(content_new)) // len(old_chair_y)
        content = content_new

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f'[TABLES_OK] {file_path}: adjusted {count_mesa} tables and {count_cadeira} chairs.')

if __name__ == '__main__':
    fix_character_prefabs()
    fix_tables_in_file('ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab')
    fix_tables_in_file('ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab')
    fix_tables_in_file('ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity')
