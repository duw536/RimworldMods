using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace KnifePistolMod
{
    // =========================================================
    // 1. XML에서 동적 텍스처 경로를 읽어오는 Properties
    // =========================================================

    public class CompProperties_DynamicTexture : CompProperties
    {
        public string meleeTexPath;
        public string rangedTexPath;

        public CompProperties_DynamicTexture()
        {
            compClass = typeof(CompDynamicTexture);
        }
    }


    // =========================================================
    // 2. 무기에 붙는 동적 텍스처 Component
    // =========================================================

    public class CompDynamicTexture : ThingComp
    {
        public CompProperties_DynamicTexture Props
        {
            get
            {
                return (CompProperties_DynamicTexture)props;
            }
        }

        private Graphic meleeGraphic;
        private Graphic rangedGraphic;

        private bool isMeleeMode = false;


        // ---------------------------------------------------------
        // 저장 / 불러오기
        // ---------------------------------------------------------

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(
                ref isMeleeMode,
                "isMeleeMode",
                false
            );
        }


        // ---------------------------------------------------------
        // 그래픽 초기화
        // ---------------------------------------------------------

        public void InitGraphics()
        {
            if (meleeGraphic == null && !Props.meleeTexPath.NullOrEmpty())
            {
                meleeGraphic = GraphicDatabase.Get<Graphic_Single>(
                    Props.meleeTexPath,
                    parent.def.graphicData.shaderType.Shader,
                    parent.def.graphicData.drawSize,
                    parent.DrawColor
                );
            }

            if (rangedGraphic == null && !Props.rangedTexPath.NullOrEmpty())
            {
                rangedGraphic = GraphicDatabase.Get<Graphic_Single>(
                    Props.rangedTexPath,
                    parent.def.graphicData.shaderType.Shader,
                    parent.def.graphicData.drawSize,
                    parent.DrawColor
                );
            }
        }


        // ---------------------------------------------------------
        // 현재 사용할 Graphic
        // ---------------------------------------------------------

        public Graphic CurrentGraphic
        {
            get
            {
                InitGraphics();

                if (isMeleeMode && meleeGraphic != null)
                {
                    return meleeGraphic;
                }

                return rangedGraphic ?? parent.def.graphicData.Graphic;
            }
        }


        // ---------------------------------------------------------
        // 근접 / 원거리 모드 변경
        // ---------------------------------------------------------

        public void SetMeleeMode(bool melee)
        {
            if (isMeleeMode == melee)
                return;

            isMeleeMode = melee;

            RefreshGraphic();
        }


        // ---------------------------------------------------------
        // 그래픽 갱신
        // ---------------------------------------------------------

        private void RefreshGraphic()
        {
            if (parent is ThingWithComps equipment)
            {
                if (equipment.ParentHolder is Pawn pawn)
                {
                    pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                }
            }
        }


        // ---------------------------------------------------------
        // 현재 근접 모드인지 확인
        // ---------------------------------------------------------

        public bool IsMeleeMode
        {
            get
            {
                return isMeleeMode;
            }
        }
    }


    // =========================================================
    // 3. Harmony 초기화
    // =========================================================

    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            Harmony harmony = new Harmony("duw536.KnifePistol");

            harmony.PatchAll();
        }
    }


    // =========================================================
    // 4. Thing.Graphic을 동적 Graphic으로 교체
    // =========================================================

    [HarmonyPatch(typeof(Thing), "Graphic", MethodType.Getter)]
    public static class Patch_Thing_Graphic
    {
        public static void Postfix(
            Thing __instance,
            ref Graphic __result)
        {
            if (__instance is ThingWithComps thingWithComps)
            {
                CompDynamicTexture comp =
                    thingWithComps.GetComp<CompDynamicTexture>();

                if (comp != null)
                {
                    __result = comp.CurrentGraphic;
                }
            }
        }
    }


    // =========================================================
    // 5. 명령을 내리는 즉시 근접 / 원거리 이미지 변경
    // =========================================================

    [HarmonyPatch(
        typeof(Pawn_JobTracker),
        nameof(Pawn_JobTracker.StartJob)
    )]
    public static class Patch_Pawn_JobTracker_StartJob
    {
        public static void Prefix(
            Pawn_JobTracker __instance,
            Job newJob,
            Pawn ___pawn)
        {
            if (___pawn == null || newJob == null)
                return;

            ThingWithComps weapon =
                ___pawn.equipment?.Primary;

            if (weapon == null)
                return;

            CompDynamicTexture comp =
                weapon.GetComp<CompDynamicTexture>();

            if (comp == null)
                return;


            // ---------------------------------------------
            // 근접 공격 명령
            // ---------------------------------------------

            if (newJob.def == JobDefOf.AttackMelee)
            {
                comp.SetMeleeMode(true);
            }


            // ---------------------------------------------
            // 원거리 공격 명령
            // ---------------------------------------------

            else if (newJob.def == JobDefOf.AttackStatic)
            {
                comp.SetMeleeMode(false);
            }
        }
    }


    // =========================================================
    // 6. 실제 근접 공격 순간에도 칼 이미지 확인
    // =========================================================

    [HarmonyPatch(
        typeof(Verb_MeleeAttack),
        "TryCastShot"
    )]
    public static class Patch_Verb_MeleeAttack
    {
        public static void Prefix(
            Verb_MeleeAttack __instance)
        {
            if (__instance.EquipmentSource == null)
                return;

            CompDynamicTexture comp =
                __instance.EquipmentSource
                    .GetComp<CompDynamicTexture>();

            if (comp != null)
            {
                comp.SetMeleeMode(true);
            }
        }
    }


    // =========================================================
    // 7. 실제 사격 순간에도 총 이미지 확인
    // =========================================================

    [HarmonyPatch(
        typeof(Verb_Shoot),
        "TryCastShot"
    )]
    public static class Patch_Verb_Shoot
    {
        public static void Prefix(
            Verb_Shoot __instance)
        {
            if (__instance.EquipmentSource == null)
                return;

            CompDynamicTexture comp =
                __instance.EquipmentSource
                    .GetComp<CompDynamicTexture>();

            if (comp != null)
            {
                comp.SetMeleeMode(false);
            }
        }
    }


    // =========================================================
    // 8. Job 종료 / 취소 시 총 이미지로 복귀
    // =========================================================

    [HarmonyPatch(
        typeof(Pawn_JobTracker),
        nameof(Pawn_JobTracker.EndCurrentJob)
    )]
    public static class Patch_Pawn_JobTracker_EndCurrentJob
    {
        public static void Prefix(
            Pawn_JobTracker __instance,
            JobCondition condition,
            Pawn ___pawn)
        {
            if (___pawn == null)
                return;

            ThingWithComps weapon =
                ___pawn.equipment?.Primary;

            if (weapon == null)
                return;

            CompDynamicTexture comp =
                weapon.GetComp<CompDynamicTexture>();

            if (comp == null)
                return;


            // ---------------------------------------------
            // 공격 Job이 끝나면 총 이미지로 복귀
            // ---------------------------------------------

            if (comp.IsMeleeMode)
            {
                comp.SetMeleeMode(false);
            }
        }
    }
}