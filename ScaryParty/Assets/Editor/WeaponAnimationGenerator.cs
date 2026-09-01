using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// Gera os 18 AnimationClips de arma diretamente como YAML Unity.
/// Versão 3 — valores de músculo recalibrados após testes visuais.
///
/// CONVENÇÃO Unity Humanoid (músculo = -1..+1):
///   Down-Up     : -1 = braço/joelho pra baixo   | +1 = braço/joelho pra cima (acima da cabeça)
///   Front-Back  : -1 = braço pra trás do corpo   | +1 = braço à frente do corpo
///   Forearm Str : -1 = cotovelo totalmente dobrado| +1 = totalmente estendido
///   Hand D-U    : -1 = pulso pra baixo (flectido) | +1 = pulso pra cima
///   Spine F-B   : -1 = coluna dobrada pra trás    | +1 = coluna dobrada pra frente
/// </summary>
public class WeaponAnimationGenerator
{
    private const string OutputDir = "Assets/Animations/Weapons/Generated";

    // Muscle attribute IDs confirmados do PlayerArmature_armature_map.json
    private static readonly (string name, int id)[] MuscleMap =
    {
        ("Spine Front-Back",       42),
        ("Spine Left-Right",       90),
        ("Chest Front-Back",       93),
        ("Chest Left-Right",       95),
        ("Head Nod Down-Up",       81),
        ("Head Tilt Left-Right",   84),
        ("Right Arm Down-Up",      43),
        ("Right Arm Front-Back",   45),
        ("Right Arm Twist In-Out", 46),
        ("Right Forearm Stretch",  54),
        ("Right Hand Down-Up",     55),
        ("Right Hand In-Out",      91),
        ("Left Arm Down-Up",       92),
        ("Left Arm Front-Back",    96),
        ("Left Arm Twist In-Out",  82),
        ("Left Forearm Stretch",   83),
    };

    // ─── POSES RECALIBRADAS ──────────────────────────────────────────────────
    //
    // REFERÊNCIA: T-Pose tem braços esticados para os lados (Down-Up = 0).
    //   Down-Up = -0.8  → braço quase na vertical para baixo (natural ao lado do corpo)
    //   Down-Up =  0.0  → braço esticado na horizontal (T-Pose)
    //   Down-Up = +0.5  → braço levantado ~45° acima da horizontal
    //   Front-Back = 0  → braço ao lado (T-Pose)
    //   Front-Back = 0.8→ braço apontado quase inteiramente para frente

    /// <summary>
    /// Low Ready — pistola apontada ~30° para frente-baixo.
    /// Mão direita à altura do peito, cotovelo dobrado.
    /// </summary>
    private static MusclePose PistolIdlePose(bool tight = false) => new MusclePose
    {
        SpineFrontBack      =  0.05f,   // leve lean pra frente
        ChestFrontBack      =  0.04f,

        // Braço direito: desce da T-Pose (-0.25), vai para frente (+0.45)
        RightArmDownUp      = -0.25f,
        RightArmFrontBack   =  tight ? 0.35f : 0.45f,
        RightArmTwistInOut  =  0.10f,
        RightForearmStretch = -0.45f,   // cotovelo ~90° dobrado
        RightHandDownUp     = -0.08f,   // pulso levemente abaixado (grip)
        RightHandInOut      = -0.05f,

        // Braço esquerdo: levemente à frente e relaxado
        LeftArmDownUp       = -0.55f,   // braço abaixado naturalmente
        LeftArmFrontBack    =  0.08f,
        LeftForearmStretch  =  0.05f,   // quase estendido, relaxado
    };

    /// <summary>
    /// ADS pistola — ambos braços estendidos à altura dos olhos.
    /// </summary>
    private static MusclePose PistolAimPose() => new MusclePose
    {
        SpineFrontBack      =  0.10f,   // lean agressivo para frente
        ChestFrontBack      =  0.08f,
        HeadNodDownUp       =  0.10f,   // cabeça levemente para frente (mira)

        // Braço direito: quase horizontal, apontando para frente
        RightArmDownUp      = -0.05f,   // quase horizontal (levemente abaixo da T-Pose)
        RightArmFrontBack   =  0.80f,   // bem para frente
        RightArmTwistInOut  = -0.05f,
        RightForearmStretch =  0.25f,   // braço maioritariamente estendido
        RightHandDownUp     =  0.05f,
        RightHandInOut      =  0.00f,

        // Braço esquerdo: espelhado para suportar a pistola
        LeftArmDownUp       = -0.05f,
        LeftArmFrontBack    =  0.78f,
        LeftArmTwistInOut   =  0.30f,   // rotação para segurar por baixo
        LeftForearmStretch  =  0.20f,
    };

    /// <summary>
    /// Port Arms — escopeta na frente do peito, cano ~45° para cima.
    /// Braços cruzados na frente segurando a arma.
    /// </summary>
    private static MusclePose ShotgunIdlePose(bool tight = false) => new MusclePose
    {
        SpineFrontBack      = -0.03f,   // leve lean para trás (peso da arma)

        // Braço direito: segurado na altura do peito
        RightArmDownUp      = -0.22f,
        RightArmFrontBack   =  0.38f,
        RightArmTwistInOut  = -0.15f,
        RightForearmStretch = -0.62f,   // cotovelo muito dobrado (coronha)
        RightHandDownUp     = -0.05f,

        // Braço esquerdo: esticado para segurar o cano/guardamão
        LeftArmDownUp       = -0.18f,
        LeftArmFrontBack    =  tight ? 0.48f : 0.55f,
        LeftArmTwistInOut   =  0.12f,
        LeftForearmStretch  = -0.32f,   // moderadamente dobrado
    };

    /// <summary>
    /// ADS escopeta — coronha no ombro direito, cheek weld.
    /// </summary>
    private static MusclePose ShotgunAimPose() => new MusclePose
    {
        SpineFrontBack      =  0.12f,
        SpineLeftRight      = -0.08f,   // leve rotação do torso para o ombro dominante
        ChestLeftRight      = -0.12f,
        HeadNodDownUp       =  0.12f,
        HeadTiltLeftRight   =  0.12f,   // cheek weld — cabeça inclina no coronha

        // Braço direito: elbow up (cotovelo levantado), empunhando
        RightArmDownUp      = -0.10f,   // cotovelo na altura do ombro
        RightArmFrontBack   =  0.42f,
        RightForearmStretch = -0.58f,

        // Braço esquerdo: esticado segurando o cano
        LeftArmDownUp       = -0.08f,
        LeftArmFrontBack    =  0.72f,
        LeftArmTwistInOut   =  0.22f,
        LeftForearmStretch  =  0.10f,   // quase estendido alcançando o cano
    };

    // ─── POSE DE HOLSTER (usada para Equip/Unequip) ─────────────────────────
    //
    // Braço ao lado do corpo, levemente para trás como num coldre de quadril.
    private static MusclePose HolsterPose() => new MusclePose
    {
        // Braço direito ABAIXADO ao lado do corpo
        RightArmDownUp      = -0.78f,   // braço caindo para baixo (CORRETO — não +1!)
        RightArmFrontBack   = -0.05f,   // levemente atrás do corpo (coldre)
        RightArmTwistInOut  =  0.05f,
        RightForearmStretch = -0.15f,   // levemente dobrado (natural)
        RightHandDownUp     = -0.05f,

        LeftArmDownUp       = -0.72f,   // braço esquerdo também ao lado
        LeftArmFrontBack    =  0.05f,
    };

    // ─── Entry point ─────────────────────────────────────────────────────────

    [MenuItem("Tools/Weapons/Generate Placeholder Animations")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(
            Path.Combine(Application.dataPath, "../", OutputDir));

        // Pistola
        WriteIdleClip    ("Pistol_Idle",    2.0f, PistolIdlePose(),        breathAmp: 0.012f);
        WriteWalkClip    ("Pistol_Walk",    0.8f, PistolIdlePose(),        bobAmp: 0.030f);
        WriteWalkClip    ("Pistol_Run",     0.6f, PistolIdlePose(true),    bobAmp: 0.055f);
        WriteIdleClip    ("Pistol_AimIdle", 1.0f, PistolAimPose(),         breathAmp: 0.006f);
        WriteWalkClip    ("Pistol_AimWalk", 0.8f, PistolAimPose(),         bobAmp: 0.015f);
        WriteRecoilClip  ("Pistol_Fire",    0.3f, PistolIdlePose(),        recoilMag: 0.08f);
        WriteRecoilClip  ("Pistol_AimFire", 0.25f, PistolAimPose(),        recoilMag: 0.05f);
        WriteEquipClip   ("Pistol_Equip",   0.5f, PistolIdlePose());
        WriteUnequipClip ("Pistol_Unequip", 0.4f, PistolIdlePose());

        // Escopeta
        WriteIdleClip    ("Shotgun_Idle",    2.0f, ShotgunIdlePose(),      breathAmp: 0.012f);
        WriteWalkClip    ("Shotgun_Walk",    0.8f, ShotgunIdlePose(),      bobAmp: 0.030f);
        WriteWalkClip    ("Shotgun_Run",     0.6f, ShotgunIdlePose(true),  bobAmp: 0.055f);
        WriteIdleClip    ("Shotgun_AimIdle", 1.0f, ShotgunAimPose(),       breathAmp: 0.006f);
        WriteWalkClip    ("Shotgun_AimWalk", 0.8f, ShotgunAimPose(),       bobAmp: 0.015f);
        WriteRecoilClip  ("Shotgun_Fire",    0.5f, ShotgunIdlePose(),      recoilMag: 0.18f);
        WriteRecoilClip  ("Shotgun_AimFire", 0.45f, ShotgunAimPose(),      recoilMag: 0.12f);
        WriteEquipClip   ("Shotgun_Equip",   0.7f, ShotgunIdlePose());
        WriteUnequipClip ("Shotgun_Unequip", 0.5f, ShotgunIdlePose());

        AssetDatabase.Refresh();
        Debug.Log($"✅ 18 AnimationClips gerados em {OutputDir}");
    }

    // ─── Clip writers ─────────────────────────────────────────────────────────

    private static void WriteIdleClip(string name, float dur, MusclePose pose, float breathAmp)
    {
        var curves = new ClipCurves();
        foreach (var m in MuscleMap)
            curves.Add(m.name, m.id, ConstantCurve(pose.Get(m.name), dur));

        // Respiração suave somente no Chest Front-Back
        curves.Override("Chest Front-Back", 93,
            BreathingCurve(pose.ChestFrontBack, breathAmp, dur));

        WriteClip(name, dur, loop: true, curves);
    }

    private static void WriteWalkClip(string name, float dur, MusclePose pose, float bobAmp)
    {
        var curves = new ClipCurves();
        foreach (var m in MuscleMap)
            curves.Add(m.name, m.id, ConstantCurve(pose.Get(m.name), dur));

        // Bob lateral da coluna
        curves.Override("Spine Left-Right", 90,
            SineCurve(pose.SpineLeftRight, bobAmp * 0.4f, dur));
        // Bob vertical do braço direito (pequeno)
        curves.Override("Right Arm Down-Up", 43,
            SineCurve(pose.RightArmDownUp, bobAmp * 0.3f, dur, phase: 0f));
        // Bob braço esquerdo (fase oposta)
        curves.Override("Left Arm Down-Up", 92,
            SineCurve(pose.LeftArmDownUp, bobAmp * 0.3f, dur, phase: Mathf.PI));

        WriteClip(name, dur, loop: true, curves);
    }

    private static void WriteRecoilClip(string name, float dur, MusclePose basePose, float recoilMag)
    {
        float peakT = dur * 0.18f;

        float ramp  =  recoilMag / peakT;           // tangente de subida (íngreme)
        float decay = -recoilMag / (dur - peakT);   // tangente de descida (suave)

        var curves = new ClipCurves();
        foreach (var m in MuscleMap)
        {
            float b = basePose.Get(m.name);
            AnimationCurve c;
            switch (m.name)
            {
                // Braços SOBEM no recuo (Down-Up aumenta pois o cano empurra pra cima)
                case "Right Arm Down-Up":
                    c = RecoilCurve(b, b + recoilMag * 0.50f, dur, peakT,  ramp, decay);
                    break;
                case "Left Arm Down-Up":
                    c = RecoilCurve(b, b + recoilMag * 0.45f, dur, peakT,  ramp, decay);
                    break;
                // Cotovelo estende ligeiramente no recuo
                case "Right Forearm Stretch":
                    c = RecoilCurve(b, b + recoilMag * 0.80f, dur, peakT,  ramp, decay);
                    break;
                case "Left Forearm Stretch":
                    c = RecoilCurve(b, b + recoilMag * 0.70f, dur, peakT,  ramp, decay);
                    break;
                // Pulso snap para cima
                case "Right Hand Down-Up":
                    c = RecoilCurve(b, b + recoilMag * 0.70f, dur, peakT,  ramp, decay);
                    break;
                // Coluna vai para TRÁS (sinal negativo no ramp)
                case "Spine Front-Back":
                    c = RecoilCurve(b, b - recoilMag * 0.40f, dur, peakT, -ramp, -decay);
                    break;
                default:
                    c = ConstantCurve(b, dur);
                    break;
            }
            curves.Add(m.name, m.id, c);
        }
        WriteClip(name, dur, loop: false, curves);
    }

    private static void WriteEquipClip(string name, float dur, MusclePose targetPose)
    {
        var holster = HolsterPose();
        var curves  = new ClipCurves();

        foreach (var m in MuscleMap)
        {
            float sv = holster.Get(m.name);
            float ev = targetPose.Get(m.name);
            var c = Mathf.Abs(sv - ev) < 0.005f
                ? ConstantCurve(ev, dur)
                : LerpCurve(sv, ev, dur, easeIn: true);
            curves.Add(m.name, m.id, c);
        }
        WriteClip(name, dur, loop: false, curves);
    }

    private static void WriteUnequipClip(string name, float dur, MusclePose startPose)
    {
        var holster = HolsterPose();
        var curves  = new ClipCurves();

        foreach (var m in MuscleMap)
        {
            float sv = startPose.Get(m.name);
            float ev = holster.Get(m.name);
            var c = Mathf.Abs(sv - ev) < 0.005f
                ? ConstantCurve(sv, dur)
                : LerpCurve(sv, ev, dur, easeIn: false);
            curves.Add(m.name, m.id, c);
        }
        WriteClip(name, dur, loop: false, curves);
    }

    // ─── Curve factories ──────────────────────────────────────────────────────

    private static AnimationCurve ConstantCurve(float v, float dur) =>
        new AnimationCurve(new Keyframe(0f, v, 0f, 0f), new Keyframe(dur, v, 0f, 0f));

    private static AnimationCurve BreathingCurve(float baseVal, float amp, float dur)
    {
        float mid = dur * 0.5f;
        return new AnimationCurve(
            new Keyframe(0f,   baseVal, 0f, 0f),
            new Keyframe(mid,  baseVal + amp, 0f, 0f),
            new Keyframe(dur,  baseVal, 0f, 0f));
    }

    private static AnimationCurve SineCurve(float baseVal, float amp, float dur, float phase = 0f)
    {
        int steps = 8;
        var keys  = new Keyframe[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t  = dur * i / steps;
            float v  = baseVal + Mathf.Sin(Mathf.PI * 2f * i / steps + phase) * amp;
            keys[i]  = new Keyframe(t, v, 0f, 0f);
        }
        return new AnimationCurve(keys);
    }

    private static AnimationCurve RecoilCurve(
        float baseVal, float peakVal, float dur, float peakT,
        float ramp, float decay)
    {
        return new AnimationCurve(
            new Keyframe(0f,    baseVal,  0f,    ramp),
            new Keyframe(peakT, peakVal,  ramp,  decay),
            new Keyframe(dur,   baseVal,  -decay, 0f));
    }

    private static AnimationCurve LerpCurve(float from, float to, float dur, bool easeIn)
    {
        float slope = (to - from) / dur;
        float outT  = easeIn  ? slope * 0.15f : slope;
        float inT   = !easeIn ? slope * 0.15f : slope;
        return new AnimationCurve(
            new Keyframe(0f,  from, 0f,  outT),
            new Keyframe(dur, to,   inT, 0f));
    }

    // ─── YAML writer ──────────────────────────────────────────────────────────

    private static void WriteClip(string name, float dur, bool loop, ClipCurves curves)
    {
        var sb = new StringBuilder();
        sb.AppendLine("%YAML 1.1");
        sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
        sb.AppendLine("--- !u!74 &7400000");
        sb.AppendLine("AnimationClip:");
        sb.AppendLine("  m_ObjectHideFlags: 0");
        sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
        sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
        sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
        sb.AppendLine($"  m_Name: {name}");
        sb.AppendLine("  serializedVersion: 7");
        sb.AppendLine("  m_Legacy: 0");
        sb.AppendLine("  m_Compressed: 0");
        sb.AppendLine("  m_UseHighQualityCurve: 1");
        sb.AppendLine("  m_RotationCurves: []");
        sb.AppendLine("  m_CompressedRotationCurves: []");
        sb.AppendLine("  m_EulerCurves: []");
        sb.AppendLine("  m_PositionCurves: []");
        sb.AppendLine("  m_ScaleCurves: []");
        sb.AppendLine("  m_FloatCurves:");
        foreach (var e in curves.Entries) AppendCurve(sb, e.Name, e.Curve);
        sb.AppendLine("  m_PPtrCurves: []");
        sb.AppendLine($"  m_SampleRate: 30");
        sb.AppendLine($"  m_WrapMode: {(loop ? 2 : 0)}");
        sb.AppendLine("  m_Bounds:");
        sb.AppendLine("    m_Center: {x: 0, y: 0, z: 0}");
        sb.AppendLine("    m_Extent: {x: 0, y: 0, z: 0}");
        sb.AppendLine("  m_ClipBindingConstant:");
        sb.AppendLine("    genericBindings:");
        foreach (var e in curves.Entries) AppendBinding(sb, e.AttributeId);
        sb.AppendLine("    pptrCurveMapping: []");
        sb.AppendLine("  m_AnimationClipSettings:");
        sb.AppendLine("    serializedVersion: 2");
        sb.AppendLine("    m_AdditiveReferencePoseClip: {fileID: 0}");
        sb.AppendLine("    m_AdditiveReferencePoseTime: 0");
        sb.AppendLine("    m_StartTime: 0");
        sb.AppendLine($"    m_StopTime: {F(dur)}");
        sb.AppendLine("    m_OrientationOffsetY: 0");
        sb.AppendLine("    m_Level: 0");
        sb.AppendLine("    m_CycleOffset: 0");
        sb.AppendLine("    m_HasAdditiveReferencePose: 0");
        sb.AppendLine($"    m_LoopTime: {(loop ? 1 : 0)}");
        sb.AppendLine("    m_LoopBlend: 0");
        sb.AppendLine("    m_LoopBlendOrientation: 0");
        sb.AppendLine("    m_LoopBlendPositionY: 0");
        sb.AppendLine("    m_LoopBlendPositionXZ: 0");
        sb.AppendLine("    m_KeepOriginalOrientation: 0");
        sb.AppendLine("    m_KeepOriginalPositionY: 1");
        sb.AppendLine("    m_KeepOriginalPositionXZ: 0");
        sb.AppendLine("    m_HeightFromFeet: 0");
        sb.AppendLine("    m_Mirror: 0");
        sb.AppendLine("  m_EditorCurves: []");
        sb.AppendLine("  m_EulerEditorCurves: []");
        sb.AppendLine("  m_HasGenericRootTransform: 0");
        sb.AppendLine("  m_HasMotionFloatCurves: 0");
        sb.AppendLine("  m_Events: []");

        File.WriteAllText(
            Path.Combine(Application.dataPath, "../", OutputDir, name + ".anim"),
            sb.ToString(), Encoding.UTF8);
    }

    private static void AppendCurve(StringBuilder sb, string attr, AnimationCurve c)
    {
        sb.AppendLine("  - serializedVersion: 2");
        sb.AppendLine("    curve:");
        sb.AppendLine("      serializedVersion: 2");
        sb.AppendLine("      m_Curve:");
        foreach (var k in c.keys)
        {
            sb.AppendLine("      - serializedVersion: 3");
            sb.AppendLine($"        time: {F(k.time)}");
            sb.AppendLine($"        value: {F(k.value)}");
            sb.AppendLine($"        inSlope: {F(k.inTangent)}");
            sb.AppendLine($"        outSlope: {F(k.outTangent)}");
            sb.AppendLine("        tangentMode: 0");
            sb.AppendLine("        weightedMode: 0");
            sb.AppendLine($"        inWeight: {F(k.inWeight)}");
            sb.AppendLine($"        outWeight: {F(k.outWeight)}");
        }
        sb.AppendLine("      m_PreInfinity: 2");
        sb.AppendLine("      m_PostInfinity: 2");
        sb.AppendLine("      m_RotationOrder: 4");
        sb.AppendLine($"    attribute: {attr}");
        sb.AppendLine("    path: ");
        sb.AppendLine("    classID: 95");
        sb.AppendLine("    script: {fileID: 0}");
        sb.AppendLine("    flags: 16");
    }

    private static void AppendBinding(StringBuilder sb, int attrId)
    {
        sb.AppendLine("    - serializedVersion: 2");
        sb.AppendLine("      path: 0");
        sb.AppendLine($"      attribute: {attrId}");
        sb.AppendLine("      script: {fileID: 0}");
        sb.AppendLine("      typeID: 95");
        sb.AppendLine("      customType: 8");
        sb.AppendLine("      isPPtrCurve: 0");
        sb.AppendLine("      isIntCurve: 0");
        sb.AppendLine("      isSerializeReferenceCurve: 0");
    }

    private static string F(float v) =>
        v.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);

    // ─── Tipos auxiliares ─────────────────────────────────────────────────────

    private class ClipEntry
    {
        public string Name; public int AttributeId; public AnimationCurve Curve;
    }

    private class ClipCurves
    {
        public readonly System.Collections.Generic.List<ClipEntry> Entries =
            new System.Collections.Generic.List<ClipEntry>();
        private readonly System.Collections.Generic.Dictionary<string, int> _idx =
            new System.Collections.Generic.Dictionary<string, int>();

        public void Add(string name, int id, AnimationCurve c)
        {
            _idx[name] = Entries.Count;
            Entries.Add(new ClipEntry { Name = name, AttributeId = id, Curve = c });
        }
        public void Override(string name, int id, AnimationCurve c)
        {
            if (_idx.TryGetValue(name, out int i))
                Entries[i] = new ClipEntry { Name = name, AttributeId = id, Curve = c };
            else Add(name, id, c);
        }
    }

    private class MusclePose
    {
        public float SpineFrontBack, SpineLeftRight, ChestFrontBack, ChestLeftRight;
        public float HeadNodDownUp, HeadTiltLeftRight;
        public float RightArmDownUp, RightArmFrontBack, RightArmTwistInOut;
        public float RightForearmStretch, RightHandDownUp, RightHandInOut;
        public float LeftArmDownUp, LeftArmFrontBack, LeftArmTwistInOut, LeftForearmStretch;

        public float Get(string n) => n switch
        {
            "Spine Front-Back"       => SpineFrontBack,
            "Spine Left-Right"       => SpineLeftRight,
            "Chest Front-Back"       => ChestFrontBack,
            "Chest Left-Right"       => ChestLeftRight,
            "Head Nod Down-Up"       => HeadNodDownUp,
            "Head Tilt Left-Right"   => HeadTiltLeftRight,
            "Right Arm Down-Up"      => RightArmDownUp,
            "Right Arm Front-Back"   => RightArmFrontBack,
            "Right Arm Twist In-Out" => RightArmTwistInOut,
            "Right Forearm Stretch"  => RightForearmStretch,
            "Right Hand Down-Up"     => RightHandDownUp,
            "Right Hand In-Out"      => RightHandInOut,
            "Left Arm Down-Up"       => LeftArmDownUp,
            "Left Arm Front-Back"    => LeftArmFrontBack,
            "Left Arm Twist In-Out"  => LeftArmTwistInOut,
            "Left Forearm Stretch"   => LeftForearmStretch,
            _                        => 0f,
        };
    }
}
