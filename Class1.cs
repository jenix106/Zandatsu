using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace Zandatsu
{
    public class ZandatsuModule : LevelModule
    {
        public static ZandatsuModule local;
        public bool noSpell;
        public bool noSlowMo;
        public bool noBlueTint;
        public bool noElectrolytes;
        public override IEnumerator OnLoadCoroutine()
        {
            local = this;
            EventManager.onCreatureSpawn += EventManager_onCreatureSpawn;
            //EventManager.onCreatureHit += EventManager_onCreatureHit;
            return base.OnLoadCoroutine();
        }

        private void EventManager_onCreatureHit(Creature creature, CollisionInstance collisionInstance)
        {
            if(!noElectrolytes && collisionInstance.sourceColliderGroup.collisionHandler.ragdollPart != null && collisionInstance.sourceColliderGroup.collisionHandler.ragdollPart.isSliced &&
                collisionInstance.sourceColliderGroup.collisionHandler.ragdollPart.type == RagdollPart.Type.Torso)
            {
                RagdollPart chest = collisionInstance.sourceColliderGroup.collisionHandler.ragdollPart;
                Catalog.GetData<ItemData>("Electrolytes").SpawnAsync(spawnedItem =>
                {
                    /*foreach (Collider collider in spawnedItem.GetComponentsInChildren<Collider>())
                        creature.ragdoll.IgnoreCollision(collider, true);*/
                    spawnedItem.gameObject.AddComponent<ElectrolytesComponent>();
                }, chest.transform.position + (Vector3.up * 0.3f), chest.transform.rotation, chest.transform, false);
            }
        }

        private void EventManager_onCreatureSpawn(Creature creature)
        {
            if(creature.isPlayer && noSpell && creature.gameObject.GetComponent<ZandatsuComponent>() == null)
            {
                creature.gameObject.AddComponent<ZandatsuComponent>();
            }
            if (!creature.isPlayer && creature.gameObject.GetComponent<ZandatsuEnemy>() == null && !noElectrolytes) creature.gameObject.AddComponent<ZandatsuEnemy>();
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
            Catalog.GetData<ItemData>("Electrolytes").SpawnAsync(spawnedItem =>
            {
                foreach (Collider collider in spawnedItem.GetComponentsInChildren<Collider>())
                    foreach(Collider partCollider in chest.colliderGroup.colliders)
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
                if (GameManager.slowMotionState == GameManager.SlowMotionState.Running)
                    GameManager.SetSlowMotion(false, slowmo.scale, slowmo.exitCurve);
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
            instance = Catalog.GetData<EffectData>("ElectrolyteSplash").Spawn(hand.transform, true);
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
            if (active && !ZandatsuModule.local.noSpell)
            {
                if (spellCaster.ragdollHand.creature.gameObject.GetComponent<ZandatsuComponent>() != null) GameObject.Destroy(spellCaster.ragdollHand.creature.gameObject.GetComponent<ZandatsuComponent>());
                else
                {
                    spellCaster.ragdollHand.creature.gameObject.AddComponent<ZandatsuComponent>();
                    CameraEffects.DoTimedEffect(lightBlue, CameraEffects.TimedEffect.Flash, 0.5f);
                }
            }
        }
    }
    public class ZandatsuComponent : MonoBehaviour
    {
        public bool active = false;
        public Dictionary<RagdollPart, bool> partsSlice = new Dictionary<RagdollPart, bool>();
        Color lightBlue = new Color(0.678f, 0.847f, 0.901f, 0.2f);
        public void Start()
        {
            if (ZandatsuModule.local.noSlowMo)
            {
                StartCoroutine(StartZandatsu());
                active = true;
            }
        }
        public void Update()
        {
            if (!ZandatsuModule.local.noSlowMo)
            {
                if (Time.timeScale < 1 && !active)
                {
                    StartCoroutine(StartZandatsu());
                    active = true;
                }
                else if (Time.timeScale == 1 && active)
                {
                    StartCoroutine(StopZandatsu());
                    active = false;
                }
            }
        }
        public IEnumerator StartZandatsu()
        {
            if (!ZandatsuModule.local.noBlueTint)
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
            if (!ZandatsuModule.local.noBlueTint)
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
        public void OnDestroy()
        {
            if(active)
            StartCoroutine(StopZandatsu());
            active = false;
        }
    }
}
