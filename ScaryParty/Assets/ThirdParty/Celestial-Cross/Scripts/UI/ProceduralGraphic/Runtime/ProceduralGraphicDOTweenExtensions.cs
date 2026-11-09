using UnityEngine;
using DG.Tweening;

namespace ProceduralGraphicSystem
{
    public static class ProceduralGraphicDOTweenExtensions
    {
        public static Tweener DOPoint(this ProceduralGraphic graphic, int pointIndex, Vector2 target, float duration)
        {
            return DOTween.To(
                () => graphic.GetPointPosition(pointIndex),
                x => graphic.SetPointPosition(pointIndex, x),
                target,
                duration
            ).SetTarget(graphic);
        }

        public static Tweener DOTransition(this ProceduralGraphic graphic, string keyframeName, float duration)
        {
            if (graphic.Preset == null) return null;
            ShapePreset.ShapeKeyframe targetKf = graphic.Preset.GetKeyframeByName(keyframeName);
            
            Vector2[] startPositions = new Vector2[graphic.PointCount];
            for(int i=0; i<startPositions.Length; i++) startPositions[i] = graphic.GetPointPosition(i);

            float progress = 0f;
            return DOTween.To(
                () => progress,
                t => 
                {
                    progress = t;
                    for(int i=0; i<graphic.PointCount; i++)
                    {
                        if(i < targetKf.positions.Count)
                            graphic.SetPointPosition(i, Vector2.Lerp(startPositions[i], targetKf.positions[i], t));
                    }
                },
                1f,
                duration
            ).SetTarget(graphic);
        }

        public static Tweener DOBlendPreset(this ProceduralGraphic graphic, ShapePreset targetPreset, float duration)
        {
            Vector2[] startPositions = new Vector2[graphic.PointCount];
            for(int i=0; i<startPositions.Length; i++) startPositions[i] = graphic.GetPointPosition(i);

            float progress = 0f;
            return DOTween.To(
                () => progress,
                t => 
                {
                    progress = t;
                    for(int i=0; i<graphic.PointCount; i++)
                    {
                        if(i < targetPreset.Points.Count)
                            graphic.SetPointPosition(i, Vector2.Lerp(startPositions[i], targetPreset.Points[i].position, t));
                    }
                },
                1f,
                duration
            ).SetTarget(graphic);
        }

        public static Tweener DOBlendTimeline(this ProceduralGraphic graphic, float startT, float endT, float duration, bool stepped = false)
        {
            if (graphic.Preset == null || graphic.Preset.Keyframes == null || graphic.Preset.Keyframes.Count == 0) return null;

            float currentTime = startT;
            return DOTween.To(
                () => currentTime,
                t => 
                {
                    currentTime = t;
                    Vector2[] evaluated = graphic.Preset.EvaluateAtTime(t, stepped);
                    for(int i=0; i<graphic.PointCount; i++)
                    {
                        if(i < evaluated.Length)
                            graphic.SetPointPosition(i, evaluated[i]);
                    }
                },
                endT,
                duration
            ).SetTarget(graphic);
        }
    }
}
