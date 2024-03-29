using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameJam.AI
{
    [RequireComponent(typeof(Light), typeof(EnemyWeapon), typeof(AudioSource))]
    public class TurretPatrol : EnemyBase
    {
        public float patrolAngle;
        public float spotAngle;
        public float spotRange;
    
        private Quaternion patrolRotation;
        private Quaternion targetRotation;
        
        private Light spotLight;
        private EnemyWeapon weapon;
        private Player.Player player;
        private AudioSource audioSource;

        private void OnValidate() => GetComponent<Light>().spotAngle = spotAngle;

        private void Awake()
        {
            spotLight = GetComponent<Light>();
            weapon = GetComponent<EnemyWeapon>();
            weapon.ignoreColliders.AddRange(GetComponentsInChildren<Collider>());
            audioSource = GetComponent<AudioSource>();

            StateList = new()
            {
                {EnemyStateType.Patrol, (0, 20, PatrolRoutine)},
                {EnemyStateType.Alert,  (20, 65, AlertedRoutine)},
                {EnemyStateType.Engage, (65, 100, EngageRoutine)},
            };
        }

        private void Start()
        {
            patrolRotation = transform.rotation;
            spotLight.spotAngle = spotAngle;
            player = FindObjectOfType<Player.Player>();

            CurrentState = (0, EnemyStateType.Patrol, StartCoroutine(PatrolRoutine()));
        }
    
        private void FixedUpdate()
        {
            CurrentState.level += (IsPlayerSpotted() ? awareness : -memory) * Time.fixedDeltaTime;
            CurrentState.level = Mathf.Clamp(CurrentState.level, 0, 100);

            // Switch between states
            if (CurrentState.level < StateList[CurrentState.type].minLevel || CurrentState.level > StateList[CurrentState.type].maxLevel)
            {
                StopCoroutine(CurrentState.routine);
                var newState = StateList.First(x => CurrentState.level > x.Value.minLevel && CurrentState.level < x.Value.maxLevel);
                CurrentState = (CurrentState.level, newState.Key, StartCoroutine(newState.Value.routine.Invoke()));
            }
        }

        private bool IsPlayerSpotted()
        {
            var playerDirection = player.transform.position - transform.position;
            return Vector3.Angle(transform.forward, playerDirection) < spotAngle / 2
                   && Physics.Raycast(transform.position, playerDirection, out var hit, spotRange, ~LayerMask.GetMask("Turret"))
                   && hit.collider.CompareTag("Player");
        }


        private IEnumerator PatrolRoutine()
        {
            spotLight.color = Color.white;
            while (true)
            {
                var targetDirection = RandomConeDirection(patrolRotation * Vector3.forward, patrolAngle);
                yield return StartRotateTurretRoutine(ToDirection(targetDirection, 20));
                yield return new WaitForSeconds(Random.Range(2, 6));
            }
        }

        private IEnumerator AlertedRoutine()
        {
            spotLight.color = Color.yellow;
            yield return StartRotateTurretRoutine(FollowTarget(player.transform, 50));
            while (true)
                yield return null;
        }
        
        private IEnumerator EngageRoutine()
        {
            spotLight.color = Color.red;
            StartRotateTurretRoutine(LeadTarget(player.GetComponent<Rigidbody>(), 50));
            yield return new WaitForSeconds(1f);
            while (true)
            {
                var shotCount = Random.Range(3, 8);
                for (var i = 0; i < shotCount; i++)
                {
                    weapon.Shoot();
                    yield return new WaitForSeconds(0.2f);
                }
                yield return new WaitForSeconds(Random.Range(0.5f, 2f));
            }
        }


        private Coroutine currentRotateRoutine;

        private Coroutine StartRotateTurretRoutine(IEnumerator newRotateRoutine)
        {
            if(currentRotateRoutine != null)
                StopCoroutine(currentRotateRoutine);
            return currentRotateRoutine = StartCoroutine(newRotateRoutine);
        }

        private IEnumerator ToDirection(Vector3 direction, float rotateSpeed)
        {
            var directionRotation = Quaternion.LookRotation(direction);
            while (Vector3.Angle(transform.forward, direction) > 0.1f)
            {
                if(RotateTurretStep(directionRotation, rotateSpeed))
                    yield return null;
                else
                    break;
            }
        }
        
        private IEnumerator FollowTarget(Transform target, float rotateSpeed)
        {
            while (true)
            {
                RotateTurretStep(Quaternion.LookRotation(target.position - transform.position), rotateSpeed);
                yield return null;
            }
        }
        
        private IEnumerator LeadTarget(Rigidbody target, float rotateSpeed)
        {
            while (true)
            {
                var leadPos = CalculateLeadingPosition(target, weapon.shootPosition, 8f);
                RotateTurretStep(Quaternion.LookRotation(leadPos - transform.position), rotateSpeed);
                yield return null;
            }
        }
        
        private static Vector3 CalculateLeadingPosition(Rigidbody target, Vector3 shooterPosition, float projectileVelocity)
        {
            var delta = target.position - shooterPosition;
            
            // https://www.gamedeveloper.com/programming/shooting-a-moving-target
            var a = Vector3.Dot(target.velocity, target.velocity) - Mathf.Pow(projectileVelocity, 2);
            var b = 2 * Vector3.Dot(target.velocity, delta);
            var c = Vector3.Dot(delta, delta);
            var det = Mathf.Pow(b, 2) - 4 * a * c;
            var timeToHit =  2f * c / (Mathf.Sqrt(det) - b);
            
            return target.position + target.velocity * timeToHit;
        }

        private bool RotateTurretStep(Quaternion rotation, float rotateSpeed)
        {
            var newRot = Quaternion.RotateTowards(transform.rotation, rotation, rotateSpeed * Time.deltaTime);
            
            // Clamp to patrol angle
            var newRotAngle = Vector3.Angle(newRot * Vector3.forward, patrolRotation * Vector3.forward);
            var currentAngle = Vector3.Angle(transform.rotation * Vector3.forward, patrolRotation * Vector3.forward);
            if (newRotAngle <= patrolAngle / 2 || newRotAngle < currentAngle)
            {
                transform.rotation = newRot;
                return true;
            }

            return false;
        }

        private static Vector3 RandomConeDirection(Vector3 direction, float angle) => Quaternion.Euler(Random.insideUnitCircle * angle) * direction;
    }

}

