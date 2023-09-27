using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace Zandatsu
{
    public class ZandatsuModule : ThunderScript
    {
        [ModOption(name: "No Spell", tooltip: "Removes the need to cast the spell", valueSourceName: nameof(booleanOption), defaultValueIndex = 1)]
        public static bool noSpell = false;
        [ModOption(name: "No Slow-Motion", tooltip: "Removes the need to activate slow-motion", valueSourceName: nameof(booleanOption), defaultValueIndex = 1)]
        public static bool noSlowMo = false;
        [ModOption(name: "No Blue Tint", tooltip: "Removes the blue tint when activated", valueSourceName: nameof(booleanOption), defaultValueIndex = 1)]
        public static bool noBlueTint = false;
        [ModOption(name: "No Electrolytes", tooltip: "Removes the electrolytes when you slice through an enemy's torso", valueSourceName: nameof(booleanOption), defaultValueIndex = 1)]
        public static bool noElectrolytes = false;
        public static bool spellActive = false;
        public static ZandatsuComponent component;
        public override void ScriptEnable()
        {
            base.ScriptEnable();
            EventManager.onCreatureSpawn += EventManager_onCreatureSpawn;
        }
        public override void ScriptDisable()
        {
            base.ScriptDisable();
            EventManager.onCreatureSpawn -= EventManager_onCreatureSpawn;
        }
        public static ModOptionBool[] booleanOption =
        {
            new ModOptionBool("Enabled", true),
            new ModOptionBool("Disabled", false)
        };

        private void EventManager_onCreatureSpawn(Creature creature)
        {
            if(creature.isPlayer && creature.gameObject.GetComponent<ZandatsuComponent>() == null)
            {
                component = creature.gameObject.AddComponent<ZandatsuComponent>();
            }
            if (!creature.isPlayer && creature.gameObject.GetComponent<ZandatsuEnemy>() == null) creature.gameObject.AddComponent<ZandatsuEnemy>();
        }
    }
    public class ZandatsuEnemy : MonoBehaviour
    {
        Creature creature;
        public void Start()
        {
            creature = GetComponent<Creature>();
            creature.ragdoll.OnSliceEvent += Ragdoll_OnSliceEvent;
        }

        private void Ragdoll_OnSliceEvent(RagdollPart ragdollPart, EventTime eventTime)
        {
            if(ragdollPart.type == RagdollPart.Type.Torso && eventTime == EventTime.OnStart)
            {
                StartCoroutine(SpawnElectrolytes(ragdollPart));
                creature.ragdoll.OnSliceEvent -= Ragdoll_OnSliceEvent;
            }
        }
        public IEnumerator SpawnElectrolytes(RagdollPart chest)
        {
            if (!ZandatsuModule.noElectrolytes)
                Catalog.GetData<ItemData>("Electrolytes").SpawnAsync(spawnedItem =>
                {
                    foreach (Collider collider in spawnedItem.GetComponentsInChildren<Collider>())
                        foreach (Collider partCollider in chest.colliderGroup.colliders)
                        {
                            Physics.IgnoreCollision(collider, partCollider, true);
                        }
                    spawnedItem.gameObject.AddComponent<ElectrolytesComponent>();
                }, chest.transform.position + (Vector3.up * 0.3f), chest.transform.rotation, chest.transform, false);
            Destroy(this);
            yield break;
        }
    }
    public class ElectrolytesComponent : MonoBehaviour
    {
        Item item;
        SpellPowerSlowTime slowmo;
        public void Start()
        {
            item = GetComponent<Item>();
            item.OnHeldActionEvent += Item_OnHeldActionEvent;
            slowmo = Player.local.creature.mana.GetPowerSlowTime();
        }

        private void Item_OnHeldActionEvent(RagdollHand ragdollHand, Handle handle, Interactable.Action action)
        {
            if(action == Interactable.Action.UseStart)
            {
                ragdollHand.creature.Heal(Mathf.Max(0, ragdollHand.creature.maxHealth - ragdollHand.creature.currentHealth), ragdollHand.creature);
                ragdollHand.creature.mana.currentMana += Mathf.Max(0, ragdollHand.creature.mana.maxMana - ragdollHand.creature.mana.currentMana);
                ragdollHand.creature.mana.currentFocus += Mathf.Max(0, ragdollHand.creature.mana.maxFocus - ragdollHand.creature.mana.currentFocus);
                ragdollHand.gameObject.AddComponent<ElectrolyteSplash>();
                CameraEffects.DoTimedEffect(Color.blue, CameraEffects.TimedEffect.Flash, 0.5f);
                if (TimeManager.slowMotionState == TimeManager.SlowMotionState.Running)
                    TimeManager.SetSlowMotion(false, slowmo.scale, slowmo.exitCurve);
                item.Despawn();
            }
        }
    }
    public class ElectrolyteSplash : MonoBehaviour
    {
        RagdollHand hand;
        EffectInstance instance;
        public void Start()
        {
            hand = GetComponent<RagdollHand>();
            instance = Catalog.GetData<EffectData>("ElectrolyteSplash").Spawn(hand.transform, null, true);
            instance.SetIntensity(1f);
            instance.Play();
            Destroy(this, 2);
        }
    }
    public class ZandatsuSpell : SpellCastCharge
    {
        Color lightBlue = new Color(0.678f, 0.847f, 0.901f, 0.2f);
        public override void Fire(bool active)
        {
            base.Fire(active);
            if (active && !ZandatsuModule.noSpell)
            {
                ZandatsuModule.spellActive = !ZandatsuModule.spellActive;
                if (ZandatsuModule.spellActive)
                    CameraEffects.DoTimedEffect(lightBlue, CameraEffects.TimedEffect.Flash, 0.5f);
            }
        }
    }
    public class ZandatsuComponent : MonoBehaviour
    {
        public bool active = false;
        public Dictionary<RagdollPart, bool> partsSlice = new Dictionary<RagdollPart, bool>();
        Color lightBlue = new Color(0.678f, 0.847f, 0.901f, 0.2f);
        public void Update()
        {
            if ((ZandatsuModule.noSlowMo || !ZandatsuModule.noSlowMo && Time.timeScale < 1) && (ZandatsuModule.noSpell || (!ZandatsuModule.noSpell && ZandatsuModule.spellActive)) && !active)
            {
                active = true;
                StartCoroutine(StartZandatsu());
            }
            else if (((!ZandatsuModule.noSlowMo && Time.timeScale == 1) || (!ZandatsuModule.noSpell && !ZandatsuModule.spellActive)) && active)
            {
                active = false;
                StartCoroutine(StopZandatsu());
            }
        }
        public IEnumerator StartZandatsu()
        {
            if (!ZandatsuModule.noBlueTint)
                CameraEffects.DoTimedEffect(lightBlue, CameraEffects.TimedEffect.FadeIn, 0.1f);
            foreach (Creature creature in Creature.all)
                foreach (RagdollPart part in creature.ragdoll.parts)
                {
                    if (!partsSlice.ContainsKey(part))
                        partsSlice.Add(part, part.sliceAllowed);
                    if (part != creature.ragdoll.rootPart && partsSlice.ContainsKey(part))
                    {
                        part.sliceAllowed = true;
                    }
                }
            yield break;
        }
        public IEnumerator StopZandatsu()
        {
            if (!ZandatsuModule.noBlueTint)
                CameraEffects.DoTimedEffect(lightBlue, CameraEffects.TimedEffect.FadeOut, 0.1f);
            foreach (Creature creature in Creature.all)
                foreach (RagdollPart part in creature.ragdoll.parts)
                {
                    if (partsSlice.ContainsKey(part))
                    {
                        part.sliceAllowed = partsSlice[part];
                    }
                }
            yield break;
        }
    }
}
