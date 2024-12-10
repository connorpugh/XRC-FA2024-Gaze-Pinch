#if BURST_PRESENT
using Unity.Burst;
#endif
using System;
using Unity.Mathematics;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Utilities;

namespace UnityEngine.XR.Interaction.Toolkit.Attachment
{
    /// <summary>
    /// Manages and controls the anchor position for an XR interaction, handling how interactables snap and follow the interactor.
    /// It applies velocity-based scaling for anchor movements and supports stabilization options.
    /// </summary>
#if BURST_PRESENT
    [BurstCompile]
#endif
    [DisallowMultipleComponent]
    public class MicroAttachController : MonoBehaviour, IInteractionAttachController
    {
        private static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");

        [SerializeField]
        [Tooltip("The transform that this anchor should follow.")]
        Transform m_TransformToFollow;

        /// <summary>
        /// Gets or sets the transform that the anchor should follow.
        /// </summary>
        public Transform transformToFollow
        {
            get => m_TransformToFollow;
            set => m_TransformToFollow = value;
        }

        [Header("Stabilization Parameters")]
        [SerializeField]
        [Tooltip("The stabilization mode for the motion of the anchor. Determines how the anchor's position and rotation are stabilized relative to the followed transform.")]
        MotionStabilizationMode m_MotionStabilizationMode = MotionStabilizationMode.WithPositionOffset;

        /// <summary>
        /// Gets or sets the stabilization mode for the motion of the anchor. Determines how the anchor's position and rotation are stabilized relative to the followed transform.
        /// </summary>
        public MotionStabilizationMode motionStabilizationMode
        {
            get => m_MotionStabilizationMode;
            set => m_MotionStabilizationMode = value;
        }

        [SerializeField]
        [Tooltip("Factor for stabilizing position. Larger values increase the range of stabilization, making the effect more pronounced over a greater distance.")]
        float m_PositionStabilization = 0.25f;

        /// <summary>
        /// Factor for stabilizing position. This value represents the maximum distance (in meters) over which position stabilization will be applied. Larger values increase the range of stabilization, making the effect more pronounced over a greater distance.
        /// </summary>
        public float positionStabilization
        {
            get => m_PositionStabilization;
            set => m_PositionStabilization = value;
        }

        [SerializeField]
        [Tooltip("Factor for stabilizing angle. Larger values increase the range of stabilization, making the effect more pronounced over a greater angle.")]
        float m_AngleStabilization = 20f;

        /// <summary>
        /// Factor for stabilizing angle. This value represents the maximum angle (in degrees) over which angle stabilization will be applied. Larger values increase the range of stabilization, making the effect more pronounced over a greater angle.
        /// </summary>
        public float angleStabilization
        {
            get => m_AngleStabilization;
            set => m_AngleStabilization = value;
        }

        [Header("Smoothing Settings")]
        [SerializeField]
        [Tooltip("If true offset will be smoothed over time in XR Origin space.")]
        bool m_SmoothOffset;

        /// <summary>
        /// If true offset will be smoothed over time in XR Origin space.
        /// May present some instability if smoothing is toggled during an interaction.
        /// </summary>
        public bool smoothOffset
        {
            get => m_SmoothOffset;
            set => m_SmoothOffset = value;
        }

        [SerializeField]
        [Tooltip("Smoothing speed for the offset anchor child.")]
        [Range(1f, 30f)]
        float m_SmoothingSpeed = 10f;

        /// <summary>
        /// Smoothing amount for the anchor's position and rotation. Higher values mean more smoothing occurs faster.
        /// </summary>
        public float smoothingSpeed
        {
            get => m_SmoothingSpeed;
            set => m_SmoothingSpeed = Mathf.Clamp(value, 1f, 30f);
        }

        [Header("Anchor Movement Parameters")]
        [SerializeField]
        [Tooltip("Whether to use distance-based velocity scaling for anchor movement.")]
        bool m_UseDistanceBasedVelocityScaling = true;

        /// <summary>
        /// Whether to use distance-based velocity scaling for anchor movement.
        /// </summary>
        public bool useDistanceBasedVelocityScaling
        {
            get => m_UseDistanceBasedVelocityScaling;
            set => m_UseDistanceBasedVelocityScaling = value;
        }
        
        [Space]
        [SerializeField]
        [Tooltip("Whether momentum is used when distance scaling is in effect.")]
        bool m_UseMomentum = true;

        /// <summary>
        /// Whether momentum is used when <see cref="useDistanceBasedVelocityScaling"/> is active.
        /// </summary>
        public bool useMomentum
        {
            get => m_UseMomentum;
            set => m_UseMomentum = value;
        }
        
        [SerializeField]
        [Tooltip("Decay scalar for momentum. Higher values will cause momentum to decay faster.")]
        [Range(0f, 10f)]
        float m_MomentumDecayScale = 1.25f;
        
        /// <summary>
        /// Decay scalar for momentum. Higher values will cause momentum to decay faster.
        /// </summary>
        public float momentumDecayScale
        {
            get => m_MomentumDecayScale;
            set => m_MomentumDecayScale = Mathf.Clamp(value, 0f, 10f);
        }
        
        [Space]
        [SerializeField]
        [Range(0f, 5f)]
        [Tooltip("Scales anchor velocity from 0 to 1 based on z-velocity's deviation below a threshold. 0 means no scaling.")]
        float m_ZVelocityRampThreshold = 0.3f;

        /// <summary>
        /// Scales anchor velocity from 0 to 1 based on z-velocity's deviation below a threshold. 0 means no scaling.
        /// </summary>
        public float zVelocityRampThreshold
        {
            get => m_ZVelocityRampThreshold;
            set => m_ZVelocityRampThreshold = Mathf.Clamp(value, 0f, 5f);
        }
        
        [SerializeField]
        [Tooltip("Adjusts the object's velocity calculation when moving towards the user. It modifies the distance-based calculation that determines the velocity scalar.")]
        [Range(0f, 2f)]
        float m_PullVelocityBias = 1f;

        /// <summary>
        /// Adjusts the object's velocity calculation when moving towards the user. 
        /// It modifies the distance-based calculation that determines the velocity scalar.
        /// <see cref="minAdditionalVelocityScalar"/>
        /// <see cref="maxAdditionalVelocityScalar"/>
        /// </summary>
        public float pullVelocityBias
        {
            get => m_PullVelocityBias;
            set => m_PullVelocityBias = Mathf.Clamp(value, 0f, 2f); 
        }

        [SerializeField]
        [Tooltip("Adjusts the object's velocity calculation when moving away from the user. It modifies the distance-based calculation that determines the velocity scalar.")]
        [Range(0f, 2f)]
        float m_PushVelocityBias = 1.25f;

        /// <summary>
        /// Adjusts the object's velocity calculation when moving away from the user. 
        /// It modifies the distance-based calculation that determines the velocity scalar.
        /// <see cref="minAdditionalVelocityScalar"/>
        /// <see cref="maxAdditionalVelocityScalar"/>
        /// </summary>
        public float pushVelocityBias
        {
            get => m_PushVelocityBias;
            set => m_PushVelocityBias = Mathf.Clamp(value, 0f, 2f); 
        }

        [SerializeField]
        [Tooltip("Minimum additional velocity scaling factor for movement, interpolated by a quad bezier curve.")]
        [Range(0f, 2f)]
        float m_MinAdditionalVelocityScalar = 0.05f;

        /// <summary>
        /// Minimum additional velocity scaling factor for movement, interpolated by a quad bezier curve.
        /// </summary>
        public float minAdditionalVelocityScalar
        {
            get => m_MinAdditionalVelocityScalar;
            set => m_MinAdditionalVelocityScalar = Mathf.Clamp(value, 0f, 2f);
        }

        [SerializeField]
        [Tooltip("Maximum additional velocity scaling factor for movement, interpolated by a quad bezier curve.")]
        [Range(0, 5f)]
        float m_MaxAdditionalVelocityScalar = 1.5f;

        /// <summary>
        /// Maximum additional velocity scaling factor for movement, interpolated by a quad bezier curve.
        /// </summary>
        public float maxAdditionalVelocityScalar
        {
            get => m_MaxAdditionalVelocityScalar;
            set => m_MaxAdditionalVelocityScalar = Mathf.Clamp(value, 0f, 5f);
        }

        /// <summary>
        /// Indicates whether the anchor currently has an offset applied.
        /// </summary>
        public bool hasOffset => m_HasOffset;

        /// <summary>
        /// Event callback used to notify when the attach controller has been updated.
        /// </summary>
        public event Action attachUpdated;

        // Offset state
        bool m_FirstMovementFrame;
        bool m_HasOffset;

        float m_StartOffsetLength;
        Vector3 m_StartLocalOffset;
        Vector3 m_NormStartOffset;
        Vector3 m_NormTargetLocalOffset;
        
        float m_Pivot;
        float m_Momentum;

        bool m_WasVelocityScalingBlocked;
        bool m_HasSelectInteractor;
        IXRSelectInteractor m_SelectInteractor;
        
        bool m_HasXROrigin;
        [Header("Micro Parameters")]
        [SerializeField] private XROrigin m_XROrigin;
        //XROrigin m_XROrigin;
        Transform m_AnchorParent;
        Transform m_AnchorChild;

        Vector3 m_LastLocalTargetPosition;
        Vector3 m_LastChildOriginSpacePosition;

        readonly AttachPointVelocityTracker m_VelocityTracker = new AttachPointVelocityTracker();

        Transform GetXROriginTransform() => InitializeXROrigin() ? m_XROrigin.Origin.transform : null;

        bool InitializeXROrigin()
        {
            // We cannot use this utility; instead, XROrigin is a serialized field
            // if (m_XROrigin == null)
            //    ComponentLocatorUtility<XROrigin>.TryFindComponent(out m_XROrigin);
            m_HasXROrigin = m_XROrigin != null;
            return m_HasXROrigin;
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected virtual void OnValidate()
        {
            var minVal = Mathf.Min(m_MinAdditionalVelocityScalar, m_MaxAdditionalVelocityScalar);
            var maxVal = Mathf.Max(m_MinAdditionalVelocityScalar, m_MaxAdditionalVelocityScalar);

            m_MinAdditionalVelocityScalar = minVal;
            m_MaxAdditionalVelocityScalar = maxVal;

            if (m_TransformToFollow == null)
                m_TransformToFollow = transform;
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected virtual void Awake()
        {
            if (m_TransformToFollow == null)
                m_TransformToFollow = transform;
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!InitializeXROrigin() && m_UseDistanceBasedVelocityScaling)
            {
                Debug.LogWarning($"Missing XR Origin. Disabling distance-based velocity scaling on this {this}.", this);
                m_UseDistanceBasedVelocityScaling = false;
            }

            m_HasSelectInteractor = TryGetComponent(out m_SelectInteractor);
        }

        // ReSharper disable once Unity.RedundantEventFunction -- For consistent method override signature in derived classes
        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected virtual void OnDisable()
        {
        }

        void SyncAnchor()
        {
            if (m_TransformToFollow == null)
                m_TransformToFollow = transform;
            m_AnchorParent.position = m_TransformToFollow.position;
            m_AnchorParent.rotation = m_TransformToFollow.rotation;
        }

        /// <inheritdoc />
        Transform IInteractionAttachController.GetOrCreateAnchorTransform(bool updateTransform)
        {
            if (m_AnchorParent == null)
            {
                var origin = GetXROriginTransform();
                var typeName = GetType().Name;

                // Capture hand name
                string handName = "";
                if (TryGetComponent(out IXRInteractor interactor))
                    handName = interactor.handedness.ToString();

                m_AnchorParent = new GameObject($"[{handName} {typeName}] Attach").transform;
                m_AnchorParent.SetParent(origin, false);
                m_AnchorParent.localPosition = Vector3.zero;
                m_AnchorParent.localRotation = Quaternion.identity;

                if (m_AnchorChild == null)
                {
                    m_AnchorChild = new GameObject($"[{handName} {typeName}] Attach Child").transform;
                    m_AnchorChild.SetParent(m_AnchorParent, false);
                    m_AnchorChild.localPosition = Vector3.zero;
                    m_AnchorChild.localRotation = Quaternion.identity;
                }
            }

            if (updateTransform)
                SyncAnchor();

            return m_AnchorChild;
        }

        /// <inheritdoc />
        void IInteractionAttachController.MoveTo(Vector3 targetWorldPosition)
        {
            SyncAnchor();
            MoveToPositionWorldPosition(targetWorldPosition);
        }

        void SyncOffset()
        {
            MoveToPositionWorldPosition(m_AnchorChild.position);
        }

        void MoveToPositionWorldPosition(Vector3 targetWorldPosition)
        {
            m_AnchorChild.position = targetWorldPosition;

            Vector3 delta = targetWorldPosition - m_AnchorParent.position;
            
            // Evaluate local start offset parameters
            m_StartLocalOffset = m_AnchorParent.InverseTransformDirection(delta);
            m_StartOffsetLength = m_StartLocalOffset.magnitude;
            // Equivalent to m_StartLocalOffset.normalized but avoids second sqrt operation.
            m_NormStartOffset = m_StartOffsetLength > Vector3.kEpsilon ? m_StartLocalOffset / m_StartOffsetLength : Vector3.zero;
            
            var upVector = m_HasXROrigin ? m_XROrigin.Origin.transform.up : Vector3.up;
            bool deltaOrthogonalToUp = Vector3.Angle(delta.normalized, upVector) > 45f;

            // When possible, we try to project the offset onto the plane orthogonal to the up vector to stabilize motion
            // If the offset is too great, we use the unmodified offset.
            var selectedWorldOffset = deltaOrthogonalToUp ? Vector3.ProjectOnPlane(delta, upVector) : delta;
            m_NormTargetLocalOffset = m_AnchorParent.InverseTransformDirection(selectedWorldOffset).normalized;
            
            m_LastLocalTargetPosition = m_AnchorChild.localPosition;

            if (m_HasXROrigin)
                m_LastChildOriginSpacePosition = m_XROrigin.Origin.transform.InverseTransformPoint(m_AnchorChild.position);

            m_Pivot = m_StartOffsetLength;
            m_HasOffset = m_StartOffsetLength > 0f;
            m_Momentum = 0f;
            m_FirstMovementFrame = true;
            m_WasVelocityScalingBlocked = false;
            // This does not work 
            // m_VelocityTracker.ResetVelocityTracking();
        }

        /// <inheritdoc />
        void IInteractionAttachController.ApplyLocalPositionOffset(Vector3 offset)
        {
            SyncAnchor();
            MoveToPositionWorldPosition(m_AnchorChild.position + m_AnchorParent.TransformDirection(offset));
        }

        /// <inheritdoc />
        void IInteractionAttachController.ApplyLocalRotationOffset(Quaternion localRotation)
        {
            m_AnchorChild.localRotation *= localRotation;
        }

        /// <inheritdoc />
        public void ResetOffset()
        {
            m_FirstMovementFrame = true;
            m_HasOffset = false;
            m_WasVelocityScalingBlocked = false;
            m_Momentum = 0f;
            m_AnchorChild.SetLocalPose(Pose.identity);
            SyncAnchor();
        }
        
        // New fields for microinteraction control
        [SerializeField] private Transform m_PinchPosition;
        [SerializeField] private Transform m_MiddlePosition;
        
        [SerializeField] private Transform m_IndexTip;
        [SerializeField] private Transform m_IndexMid;

        [SerializeField] private bool m_UsePinchPosition;

        public void setIndexScrolling(bool useIndexScrolling)
        {
            m_UsePinchPosition = !useIndexScrolling;
        }

        private void Update()
        {
            if (m_FirstMovementFrame)
            {
                Debug.Log("First movement frame is true");
            }
        }


        [SerializeField] private Transform m_HandPosition;
        
        [SerializeField] private SkinnedMeshRenderer m_HandMesh;
        private Material m_HandMaterial;
        [SerializeField] private Color m_SelectColor;
        private Color m_DefaultColor;

        // These variables save the maximum extent of the fingers; this is used to discount movements in the
        // opposite direction at max/min extent, which are inevitable when releasing from a stretched position.
        private float m_MinPinch;
        private float m_MaxPinch;
        private float m_MinScroll;
        private float m_MaxScroll;
        
        
        private Vector3 m_LastPinchVector; // used for calculating velocity
        private float m_LastT;
        

        /// <inheritdoc />
        void IInteractionAttachController.DoUpdate(float deltaTime)
        {
            if (!m_HasXROrigin)
                return;
            
            var originTransform = m_XROrigin.Origin.transform;

            
            var originUp = originTransform.up;

            // Check if we skip stabilization
            if (motionStabilizationMode == MotionStabilizationMode.Never || (motionStabilizationMode == MotionStabilizationMode.WithPositionOffset && !m_HasOffset))
                SyncAnchor();
            else
            {
                if (!hasOffset)
                {
                    XRTransformStabilizer.ApplyStabilization(ref m_AnchorParent, m_TransformToFollow, positionStabilization, angleStabilization, deltaTime);
                }
                else
                {
                    float childAnchorOffsetMagnitude = m_AnchorChild.localPosition.z;
                    float stabilizationMultiplier = (1f + childAnchorOffsetMagnitude);
                    float adjustedPositionStabilization = stabilizationMultiplier * positionStabilization;
                    float adjustedAngleStabilization = stabilizationMultiplier * angleStabilization;

                    var anchorParentWorldPos = m_AnchorParent.position;
                    var worldOffset = m_AnchorChild.position - anchorParentWorldPos;
                    var isWorldOffsetOrthogonalToUp = Vector3.Angle(worldOffset.normalized, originUp) > 45f;
                    var targetWorldOffset = isWorldOffsetOrthogonalToUp ? Vector3.ProjectOnPlane(worldOffset, originUp) : worldOffset;
                    var stabilizationTarget = anchorParentWorldPos + targetWorldOffset;

                    XRTransformStabilizer.ApplyStabilization(ref m_AnchorParent, m_TransformToFollow, stabilizationTarget, adjustedPositionStabilization, adjustedAngleStabilization, deltaTime);
                }
            }

            // Track attach point velocity
            if (m_UseDistanceBasedVelocityScaling)
                m_VelocityTracker.UpdateAttachPointVelocityData(transformToFollow, originTransform);

            if (!hasOffset)
            {
                attachUpdated?.Invoke();
                return;
            }

            if (!m_UseDistanceBasedVelocityScaling || UpdateVelocityScalingBlock())
            {
                UpdatePosition(m_LastChildOriginSpacePosition, m_StartLocalOffset, deltaTime);
                attachUpdated?.Invoke();
                return;
            }

            float3 currentLocalOffset = m_SmoothOffset ? m_LastLocalTargetPosition : m_AnchorChild.localPosition;

            float3 velocityLocal;
            if (m_FirstMovementFrame)
            {
                if (m_UsePinchPosition)
                {
                    // Get the relative position of the pinch in hand space
                    Vector3 pinchVector = m_HandPosition.worldToLocalMatrix.MultiplyPoint(m_PinchPosition.position);
                    // Save pinch vector for next time
                    m_LastPinchVector = pinchVector;
                    // Save max/min values
                    m_MinPinch = Vector3.Magnitude(pinchVector);
                    m_MaxPinch = Vector3.Magnitude(pinchVector);
                }
                else
                {
                    // Get the ray from m_IndexMid to m_IndexTip
                    Ray ray = new Ray(m_IndexMid.position, m_IndexTip.position - m_IndexMid.position);
                    // Get the position along that ray of the tip of the middle finger
                    Vector3 v = m_MiddlePosition.position - ray.origin;
                    m_LastT = Vector3.Dot(v, ray.direction);
                    // Save max/min values
                    m_MinScroll = m_LastT;
                    m_MaxScroll = m_LastT;
                }

                velocityLocal = float3.zero;
                m_FirstMovementFrame = false;
            }
            else
            {
                // Instead of calculating velocityLocal based on the world velocity, we will calculate the local
                // velocity of the pinch position relative to the hand.
                if (m_UsePinchPosition)
                {
                    // Get the relative position of the pinch in hand space
                    Vector3 pinchVector = m_HandPosition.worldToLocalMatrix.MultiplyPoint(m_PinchPosition.position);
                    // Compare between last pinch vector & this one to get velocity
                    velocityLocal = pinchVector - m_LastPinchVector;
                    // Save largest & smallest velocityLocal
                    float mag = Vector3.Magnitude(velocityLocal);
                    if (mag < m_MinPinch)
                    {
                        m_MinPinch = mag;
                    } else if (mag > m_MaxPinch)
                    {
                        m_MaxPinch = mag;
                    }
                    
                    
                    // Save pinch vector for next time
                    m_LastPinchVector = pinchVector;
                    //Debug.Log("Relative position: " + pinchVector);
                    //Debug.Log("velocityLocal: " + velocityLocal);
                    // Multiply velocity for testing purposes
                    velocityLocal *= 1000.0f;
                    
                    // Calculate proportion between min & max
                    float p = (m_MaxPinch != m_MinPinch) ? (mag - m_MinPinch) / (m_MaxPinch - m_MinPinch) : 0f;
                    //
                    Debug.Log("max is " + m_MaxPinch + ", min is " + m_MinPinch + ", p is " + p);
                }
                else
                {
                    velocityLocal = 0.0f;
                    // Get the ray from m_IndexMid to m_IndexTip
                    Ray ray = new Ray(m_IndexMid.position, (m_IndexTip.position - m_IndexMid.position).normalized);
                    // Get the position along that ray of the tip of the middle finger
                    Vector3 v = m_MiddlePosition.position - ray.origin;
                    float t = Vector3.Dot(v, ray.direction);
                    
                    // Get the projected point
                    Vector3 c = ray.origin + ray.direction * t;
                    // Get the distance from the point to the ray
                    float linedist = Vector3.Distance(m_MiddlePosition.position, c);
                    // Only change velocityLocal if middle finger is close enough to index
                    if (t is > 0 and < 1 && linedist < 0.03f)
                    {
                        m_HandMaterial.SetColor(EdgeColor, m_SelectColor);
                        // Compare between the last t and this one to get velocity
                        velocityLocal = t - m_LastT;
                        // Multiply velocity for testing purposes
                        velocityLocal *= 2000.0f;
                    }
                    else
                    {
                        m_HandMaterial.SetColor(EdgeColor, m_DefaultColor);
                    }

                    if (t < m_MinScroll)
                    {
                        m_MinScroll = t;
                    } else if (t > m_MaxScroll)
                    {
                        m_MaxScroll = t;
                    }
                    
                    
                    
                    
                    
                    // Calculate proportion between min & max
                    float p = (t - m_MinScroll) / (m_MaxScroll - m_MinScroll);
                    if (p < 0.2f)
                    {
                        if (t - m_LastT > 0.0f)
                        {
                            velocityLocal = Vector3.zero;
                            Debug.Log("Cancelled forward velocity!");
                        }
                        
                    }
                    else if (p > 0.8f)
                    {
                        if (t - m_LastT < 0.0f)
                        {
                            velocityLocal = Vector3.zero;
                            Debug.Log("Cancelled backward velocity!");
                        }
                    }
                    
                    // Save for next time
                    m_LastT = t;
                }
                

                //var velocityWorld = m_VelocityTracker.GetAttachPointVelocity(originTransform);
                //var projectedVelocityWorld = Vector3.ProjectOnPlane(velocityWorld, originUp);
                //velocityLocal = m_AnchorParent.InverseTransformDirection(projectedVelocityWorld);
            }

            ComputeAmplifiedOffset(velocityLocal, m_NormStartOffset, m_StartOffsetLength, m_NormTargetLocalOffset, currentLocalOffset, m_MinAdditionalVelocityScalar, m_MaxAdditionalVelocityScalar, m_PushVelocityBias, m_PullVelocityBias, m_ZVelocityRampThreshold, m_UseMomentum, m_MomentumDecayScale, ref m_Momentum, ref m_Pivot, deltaTime, out var newOffset);

            // Check if the new offset's z-value is less than zero in local space to reset the offset
            var newOffsetDot = math.dot(math.normalize(newOffset), m_NormStartOffset);
            if (newOffsetDot < 0.05f)
                ResetOffset();
            else
                UpdatePosition(m_LastChildOriginSpacePosition, newOffset, deltaTime);

            attachUpdated?.Invoke();
        }

        private void Start()
        {
            m_HandMaterial = m_HandMesh.materials[1];
            m_DefaultColor = m_HandMaterial.GetColor(EdgeColor);
        }

        bool UpdateVelocityScalingBlock()
        {
            if (!m_HasSelectInteractor)
                return false;

            bool shouldBlock = false;
            
            // Disable distance-based velocity scaling when target object is selected by multiple interactors
            if (m_SelectInteractor.hasSelection)
            {
                var selectedInteractable = m_SelectInteractor.interactablesSelected[0];
                if (selectedInteractable != null && selectedInteractable.interactorsSelecting.Count > 1)
                    shouldBlock = true;
            }

            // If we start blocking velocity scaling, we need to sync the offset to prevent sudden jumps
            if (shouldBlock && !m_WasVelocityScalingBlocked)
                SyncOffset();
            
            m_WasVelocityScalingBlocked = shouldBlock;
            return shouldBlock;
        }

        void UpdatePosition(Vector3 lastOriginSpacePosition, Vector3 targetLocalPosition, float deltaTime)
        {
            if (!m_SmoothOffset || !m_HasXROrigin)
            {
                m_AnchorChild.localPosition = targetLocalPosition;
                m_LastLocalTargetPosition = targetLocalPosition;
                return;
            }

            var previousWorldPosition = m_XROrigin.Origin.transform.TransformPoint(lastOriginSpacePosition);
            var newTargetWorldPosition = m_AnchorParent.TransformPoint(targetLocalPosition);

            var newWorldPosition = BurstLerpUtility.BezierLerp(previousWorldPosition, newTargetWorldPosition, m_SmoothingSpeed * deltaTime);

            m_AnchorChild.position = newWorldPosition;
            m_LastChildOriginSpacePosition = m_XROrigin.Origin.transform.InverseTransformPoint(newWorldPosition);

            m_LastLocalTargetPosition = targetLocalPosition;
        }

#if BURST_PRESENT
        [BurstCompile]
#endif
        static void ComputeAmplifiedOffset(in float3 velocityLocal, in float3 normStartLocalOffset, float startOffsetLength, in float3 normTargetLocalOffset, in float3 currentLocalOffset, float minAdditionalVelocityScalar, float maxAdditionalVelocityScalar, float pushVelocityBias, float pullVelocityBias, float zVelocityRampThreshold, bool useMomentum, float momentumDecayScale, ref float momentum, ref float pivot, float deltaTime, out float3 newOffset)        
        {
            // Calculate the Bezier scale factor
            float distanceAdjustedMinVelocityScalar = minAdditionalVelocityScalar * pivot;
            float distanceAdjustedMaxVelocityScalar = maxAdditionalVelocityScalar * pivot;

            // Determine how far away the offset currently is
            float offsetMagnitude = math.length(currentLocalOffset);
            
            // Project the local velocity on the start offset direction in local space to ensure that we only scale motion along that axis
            var projectedVelocityLocal = math.project(velocityLocal, normTargetLocalOffset);
            
            // Determine if the object is moving towards or away from the user
            var velocitySign = math.sign(math.dot(math.normalize(projectedVelocityLocal), normStartLocalOffset));
            bool isMovingAway = velocitySign > 0f;
            
            // We determine forward and back motion by using the signed magnitude of projected local velocity. 
            var zMotionLocalSpace = math.length(projectedVelocityLocal) * velocitySign;

            // We use a ratio against a pivot to get some sense of motion relative to distance from the user
            float distanceRatio = math.abs(offsetMagnitude) / pivot;
            float t = math.clamp(distanceRatio * (isMovingAway ? pushVelocityBias : pullVelocityBias), 0f, 1f);
            float movementScale = BurstLerpUtility.BezierLerp(distanceAdjustedMinVelocityScalar, distanceAdjustedMaxVelocityScalar, t);

            // If below ramp threshold, we do not amplify motion
            float rampAmount = zVelocityRampThreshold > 0f ? math.clamp(math.abs(zMotionLocalSpace) / zVelocityRampThreshold, 0f, 1f) : 1f;
            bool isAboveRampThreshold = !(rampAmount < 1f);

            // Movement magnitude collated from velocity and momentum along the motion axis, scaled by the delta time for the frame
            float movement = zMotionLocalSpace * rampAmount * (1f + movementScale) * deltaTime;

            // If movement changes direction and the change is above a threshold tolerance, reset momentum
            if (useMomentum)
            {
                const float tolerance = 0.001f; 
                float absMomentum = math.abs(momentum);
                float absMovement = math.abs(movement);

                if ((int)math.sign(momentum) != (int)math.sign(movement)
                    && math.abs(absMomentum - absMovement) > tolerance)
                {
                    if (isAboveRampThreshold)
                        momentum = movement / 2f;
                    else if (rampAmount > 0.25f)
                        momentum = 0f;
                }
                else if (isAboveRampThreshold)
                {
                    // Accumulate momentum in the direction of movement
                    momentum = math.max(absMomentum, absMovement / 2f) * math.sign(movement);
                }

                // Cutoff momentum when value is too low
                if (math.abs(momentum) < tolerance)
                    momentum = 0f;
                // Decay momentum
                else
                    momentum *= 1f - momentumDecayScale * deltaTime;
            }
            else
            {
                momentum = 0f;
            }

            // Compute new offset by scaling original offset with motion and momentum
            float newOffsetMagnitude = offsetMagnitude + movement + momentum;
            newOffset = normStartLocalOffset * newOffsetMagnitude;

            // Update pivot
            if (newOffsetMagnitude > startOffsetLength)
                pivot = newOffsetMagnitude;
            else
                pivot = math.lerp(pivot, (startOffsetLength + newOffsetMagnitude) / 2f, deltaTime * movementScale);
        }
    }
}