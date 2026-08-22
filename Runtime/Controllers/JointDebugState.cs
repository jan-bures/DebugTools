using System;
using System.Collections.Generic;
using System.Linq;
using DebugShapes;
using KSP.Game;
using KSP.Rendering;
using KSP.Sim;
using KSP.Sim.impl;
using Redux.Ecs.Structural;
using DebugTools.Utils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.Controllers
{
    public enum JointDebugSortMode
    {
        MaxUtilization,
        TreeOrder
    }

    public static class JointDebugState
    {
        private static bool _activeVesselOnly = true;
        private static bool _showAnalytical = true;
        private static bool _showLegacy;
        private static bool _showMarkers;
        private static JointDebugSortMode _sortMode = JointDebugSortMode.MaxUtilization;
        private static string _hoveredConnectionId;
        private static string _selectedConnectionId;

        public static event Action Changed;

        public static bool ActiveVesselOnly => _activeVesselOnly;
        public static bool ShowAnalytical => _showAnalytical;
        public static bool ShowLegacy => _showLegacy;
        public static bool ShowMarkers => _showMarkers;
        public static bool ShowStressGauges => Configuration.StructuralStressGauges?.Value ?? false;
        public static JointDebugSortMode SortMode => _sortMode;
        public static string HoveredConnectionId => _hoveredConnectionId;
        public static string SelectedConnectionId => _selectedConnectionId;
        public static bool PointerOverJointsWindow { get; set; }

        public static void SetActiveVesselOnly(bool value)
        {
            if (_activeVesselOnly == value) return;
            _activeVesselOnly = value;
            NotifyChanged();
        }

        public static void SetShowAnalytical(bool value)
        {
            if (_showAnalytical == value) return;
            _showAnalytical = value;
            EnsureOverlayState();
            NotifyChanged();
        }

        public static void SetShowLegacy(bool value)
        {
            if (_showLegacy == value) return;
            _showLegacy = value;
            EnsureOverlayState();
            NotifyChanged();
        }

        public static void SetShowMarkers(bool value)
        {
            if (_showMarkers == value) return;
            _showMarkers = value;
            EnsureOverlayState();
            NotifyChanged();
        }

        public static void SetShowStressGauges(bool value)
        {
            if (ShowStressGauges == value) return;
            Configuration.StructuralStressGauges.Value = value;
            PhysicsSettings.STRUCTURAL_STRESS_GAUGES_ENABLED = value;
            NotifyChanged();
        }

        public static void SetSortMode(JointDebugSortMode value)
        {
            if (_sortMode == value) return;
            _sortMode = value;
            NotifyChanged();
        }

        public static void SetHoveredConnection(string id)
        {
            if (_hoveredConnectionId == id) return;
            _hoveredConnectionId = id;
            NotifyChanged();
        }

        public static void SetSelectedConnection(string id)
        {
            if (_selectedConnectionId == id) return;
            _selectedConnectionId = id;
            NotifyChanged();
        }

        public static void ClearSelection()
        {
            bool changed = _hoveredConnectionId != null || _selectedConnectionId != null;
            _hoveredConnectionId = null;
            _selectedConnectionId = null;
            if (changed) NotifyChanged();
        }

        public static string BuildAnalyticalId(VesselComponent vessel, StructuralConnectionSnapshot connection)
        {
            return $"{vessel.SimulationObject.GlobalId}:analytical:{connection.Id}";
        }

        public static string BuildLegacyId(VesselComponent vessel, PartOwnerBehavior.JointConnection connection,
            int index)
        {
            string host = connection.host?.Model?.GlobalId.ToString() ?? connection.host?.Name ?? "host";
            string target = connection.target?.Model?.GlobalId.ToString() ?? connection.target?.Name ?? "target";
            string node = connection.AttachNode?.NodeId ?? "node";
            return $"{vessel.SimulationObject.GlobalId}:legacy:{index}:{host}:{node}:{target}";
        }

        public static Color GetStressColor(float utilization)
        {
            if (utilization >= 1f) return new Color(1f, 0.12f, 0.08f, 1f);
            if (utilization >= 0.85f) return new Color(1f, 0.28f, 0.08f, 1f);
            if (utilization >= 0.5f) return new Color(1f, 0.74f, 0.12f, 1f);
            return new Color(0.24f, 0.86f, 0.38f, 1f);
        }

        public static IEnumerable<VesselComponent> GetTargetVessels()
        {
            GameInstance game = GameManager.Instance?.Game;
            ViewController view = game?.ViewController;
            if (view == null) yield break;

            if (_activeVesselOnly)
            {
                if (view.TryGetActiveSimVessel(out VesselComponent activeVessel) && activeVessel != null)
                {
                    yield return activeVessel;
                }

                yield break;
            }

            foreach (VesselComponent vessel in view.Universe.GetAllVessels())
            {
                if (vessel != null) yield return vessel;
            }
        }

        public static VesselBehavior GetLoadedBehavior(VesselComponent vessel)
        {
            return GameManager.Instance?.Game?.ViewController?.GetBehaviorIfLoaded(vessel);
        }

        public static int CountPhysicalJoints(PartOwnerBehavior partOwner)
        {
            int count = 0;
            foreach (PartOwnerBehavior.JointConnection connection in partOwner.JointConnections)
            {
                if (connection?.Joints == null) continue;
                count += connection.Joints.Count(joint => joint != null && joint.connectedBody != null);
            }

            return count;
        }

        public static int CountPartRootRigidbodies(PartOwnerBehavior partOwner)
        {
            int count = 0;
            foreach (PartBehavior part in partOwner.Parts)
            {
                if (part != null && part.GetComponent<Rigidbody>() != null) count++;
            }

            return count;
        }

        public static string GetPartLabel(PartBehavior part)
        {
            if (part == null) return "<none>";
            string displayName = part.GetDisplayName();
            return string.IsNullOrEmpty(displayName) ? part.Name : displayName;
        }

        public static string FormatPercent(float utilization)
        {
            return $"{Mathf.Max(0f, utilization) * 100f:0.0}%";
        }

        public static string FormatLimit(float value)
        {
            if (float.IsInfinity(value)) return "inf";
            if (float.IsNaN(value)) return "n/a";
            return value >= 1000f ? $"{value / 1000f:0.0}k" : $"{value:0}";
        }

        internal static void EnsureStructuralGraphReady(PartOwnerBehavior partOwner)
        {
            if (!StructuralPhysicsService.TryGetSnapshot(partOwner, out StructuralVesselSnapshot snapshot) ||
                (!snapshot.GraphValid && partOwner.Model != null && partOwner.Model.PartCount > 0))
            {
                partOwner.RebuildStructuralGraph();
            }
        }

        internal static PartBehavior ResolvePart(PartOwnerBehavior owner, string guid)
        {
            if (owner == null || string.IsNullOrEmpty(guid) ||
                !owner.Model.TryGetPart(IGGuid.CreateIGGuid(guid), out PartComponent part))
            {
                return null;
            }

            return owner.GetPartViewComponent(part);
        }

        private static void EnsureOverlayState()
        {
            if (_showMarkers)
            {
                JointDebugOverlayManager.EnsureInstance();
            }
            else
            {
                JointDebugOverlayManager.DestroyInstance();
            }
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }

    public sealed class JointDebugOverlayManager : KerbalMonoBehaviour
    {
        private static JointDebugOverlayManager _instance;

        private readonly Dictionary<string, Marker> _markers = new();
        private readonly HashSet<string> _seenMarkers = new();
        private readonly HashSet<PartOwnerBehavior> _legacyOwners = new();

        public static void EnsureInstance()
        {
            if (_instance != null) return;

            GameObject owner = new("JointDebugOverlayManager");
            _instance = owner.AddComponent<JointDebugOverlayManager>();
            DontDestroyOnLoad(owner);
        }

        public static void DestroyInstance()
        {
            if (_instance == null) return;

            _instance.ClearAll();
            Destroy(_instance.gameObject);
            _instance = null;
        }

        private void LateUpdate()
        {
            if (!JointDebugState.ShowMarkers)
            {
                ClearAll();
                return;
            }

            UpdateAnalyticalMarkers();
            UpdateLegacyVisualJoints();
            UpdateHover();
        }

        private void OnDestroy()
        {
            ClearAll();
            if (_instance == this) _instance = null;
        }

        private void UpdateAnalyticalMarkers()
        {
            _seenMarkers.Clear();

            if (!JointDebugState.ShowAnalytical)
            {
                RemoveUnseenMarkers();
                return;
            }

            foreach (VesselComponent vessel in JointDebugState.GetTargetVessels())
            {
                VesselBehavior behavior = JointDebugState.GetLoadedBehavior(vessel);
                PartOwnerBehavior partOwner = behavior?.PartOwner;
                if (partOwner == null) continue;

                JointDebugState.EnsureStructuralGraphReady(partOwner);
                if (!StructuralPhysicsService.TryGetSnapshot(partOwner, out StructuralVesselSnapshot snapshot))
                {
                    continue;
                }

                foreach (StructuralConnectionSnapshot connection in snapshot.Connections)
                {
                    string id = JointDebugState.BuildAnalyticalId(vessel, connection);
                    _seenMarkers.Add(id);

                    if (!_markers.TryGetValue(id, out Marker marker))
                    {
                        marker = CreateMarker(id);
                        _markers[id] = marker;
                    }

                    marker.Connection = connection;
                    marker.Parent = JointDebugState.ResolvePart(partOwner, connection.ParentGuid);
                    marker.Child = JointDebugState.ResolvePart(partOwner, connection.ChildGuid);
                    marker.Center = connection.AnchorWorld;
                    marker.BaseRadius = CalculateMarkerRadius(connection);
                    UpdateMarkerVisual(marker);
                }
            }

            RemoveUnseenMarkers();
        }

        private Marker CreateMarker(string id)
        {
            var markerObject = new GameObject($"JointMarker_{id}");
            markerObject.transform.SetParent(transform);

            var sphere = new DebugSphere(
                $"JointMarker_{id}", markerObject.transform, Color.green, Vector3.zero, 0.1f)
            {
                Enabled = true
            };

            var outline = new DebugSphere(
                $"JointMarker_{id}_Outline", markerObject.transform, Color.white, Vector3.zero, 0.12f)
            {
                Enabled = false
            };

            return new Marker(id, markerObject, sphere, outline);
        }

        private void UpdateMarkerVisual(Marker marker)
        {
            bool isSelected = JointDebugState.SelectedConnectionId == marker.Id;
            bool isHovered = JointDebugState.HoveredConnectionId == marker.Id;
            float radius = marker.BaseRadius;
            Color color = JointDebugState.GetStressColor(marker.Connection?.Utilization ?? 0f);

            if (isSelected)
            {
                radius *= 1.9f;
                color = new Color(0.2f, 0.95f, 1f, 1f);
            }
            else if (isHovered)
            {
                radius *= 1.55f;
                color = Color.white;
            }

            marker.CurrentRadius = radius;
            marker.Sphere.Enabled = true;
            marker.Sphere.Radius = radius;
            marker.Sphere.Color = color;
            marker.Sphere.Center = marker.Center;

            bool showOutline = isSelected || isHovered;
            marker.OutlineSphere.Enabled = showOutline;
            if (showOutline)
            {
                marker.OutlineSphere.Radius = radius * 1.18f;
                marker.OutlineSphere.Color = isSelected
                    ? new Color(0.2f, 1f, 1f, 0.75f)
                    : new Color(1f, 1f, 1f, 0.65f);
                marker.OutlineSphere.Center = marker.Center;
            }
        }

        private void RemoveUnseenMarkers()
        {
            List<string> stale = null;
            foreach (string id in _markers.Keys)
            {
                if (!_seenMarkers.Contains(id))
                {
                    stale ??= new List<string>();
                    stale.Add(id);
                }
            }

            if (stale == null) return;

            foreach (string id in stale)
            {
                Destroy(_markers[id].GameObject);
                _markers.Remove(id);
            }
        }

        private void UpdateLegacyVisualJoints()
        {
            HashSet<PartOwnerBehavior> currentOwners = new();

            foreach (VesselComponent vessel in JointDebugState.GetTargetVessels())
            {
                VesselBehavior behavior = JointDebugState.GetLoadedBehavior(vessel);
                PartOwnerBehavior partOwner = behavior?.PartOwner;
                if (partOwner == null) continue;

                currentOwners.Add(partOwner);
                partOwner.VisualizeJoints = JointDebugState.ShowMarkers && JointDebugState.ShowLegacy;
                SetLegacyVisualJoints(partOwner, partOwner.VisualizeJoints);
            }

            foreach (PartOwnerBehavior owner in _legacyOwners)
            {
                if (!currentOwners.Contains(owner))
                {
                    SetLegacyVisualJoints(owner, false);
                    owner.VisualizeJoints = false;
                }
            }

            _legacyOwners.Clear();
            foreach (PartOwnerBehavior owner in currentOwners) _legacyOwners.Add(owner);
        }

        private static void SetLegacyVisualJoints(PartOwnerBehavior partOwner, bool enabled)
        {
            foreach (PartOwnerBehavior.JointConnection connection in partOwner.JointConnections)
            {
                if (connection?.visualJoints == null || connection.visualJoints.Length == 0) continue;

                int visualIndex = 0;
                if (connection.Joints == null)
                {
                    foreach (var sphere in connection.visualJoints)
                    {
                        if (sphere != null) sphere.Enabled = false;
                    }

                    continue;
                }

                foreach (ConfigurableJoint joint in connection.Joints)
                {
                    if (visualIndex >= connection.visualJoints.Length) break;
                    var sphere = connection.visualJoints[visualIndex++];
                    if (sphere == null) continue;

                    sphere.Enabled = enabled && joint != null && joint.connectedBody != null;
                    if (sphere.Enabled)
                    {
                        sphere.Center = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
                    }
                }

                while (visualIndex < connection.visualJoints.Length)
                {
                    var sphere = connection.visualJoints[visualIndex++];
                    if (sphere != null) sphere.Enabled = false;
                }
            }
        }

        private void UpdateHover()
        {
            if (JointDebugState.PointerOverJointsWindow)
            {
                JointDebugState.SetHoveredConnection(null);
                return;
            }

            Camera camera = GetFlightPhysicsCamera();
            if (camera == null)
            {
                JointDebugState.SetHoveredConnection(null);
                return;
            }

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            Marker best = null;
            float bestDistance = float.PositiveInfinity;

            foreach (Marker marker in _markers.Values)
            {
                if (!RaySphere(ray, marker.Center, Mathf.Max(marker.CurrentRadius, 0.18f), out float distance))
                    continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = marker;
                }
            }

            JointDebugState.SetHoveredConnection(best?.Id);
            if (best != null && Input.GetMouseButtonDown(0))
            {
                JointDebugState.SetSelectedConnection(best.Id);
            }
        }

        private static Camera GetFlightPhysicsCamera()
        {
            try
            {
                return GameManager.Instance?.Game?.CameraManager
                    ?.GetCameraRenderStack(CameraID.Flight, RenderSpaceType.PhysicsSpace)
                    ?.GetMainRenderCamera();
            }
            catch
            {
                return Camera.main;
            }
        }

        private static bool RaySphere(Ray ray, Vector3 center, float radius, out float distance)
        {
            Vector3 toCenter = center - ray.origin;
            float projection = Vector3.Dot(toCenter, ray.direction);
            float closestDistanceSquared = toCenter.sqrMagnitude - projection * projection;
            float radiusSquared = radius * radius;
            if (closestDistanceSquared > radiusSquared)
            {
                distance = 0f;
                return false;
            }

            float offset = Mathf.Sqrt(radiusSquared - closestDistanceSquared);
            distance = projection - offset;
            if (distance < 0f) distance = projection + offset;
            return distance >= 0f;
        }

        private static float CalculateMarkerRadius(StructuralConnectionSnapshot connection)
        {
            float force = float.IsFinite(connection.BreakForce) ? Mathf.Max(connection.BreakForce, 1f) : 1_000_000f;
            float torque = float.IsFinite(connection.BreakTorque) ? Mathf.Max(connection.BreakTorque, 1f) : 1_000_000f;
            float torqueEquivalent = Mathf.Sqrt(torque * 1000f);
            float strength = Mathf.Max(force, torqueEquivalent);
            float t = Mathf.InverseLerp(2f, 7f, Mathf.Log10(Mathf.Max(strength, 1f)));
            return Mathf.Lerp(0.08f, 0.38f, t);
        }

        private void ClearAll()
        {
            foreach (Marker marker in _markers.Values)
            {
                if (marker.GameObject != null) Destroy(marker.GameObject);
            }

            _markers.Clear();
            _seenMarkers.Clear();

            foreach (PartOwnerBehavior owner in _legacyOwners)
            {
                if (owner == null) continue;
                owner.VisualizeJoints = false;
                SetLegacyVisualJoints(owner, false);
            }

            _legacyOwners.Clear();
            JointDebugState.SetHoveredConnection(null);
            GameManager.Instance?.Game?.UI?.OutlineManager?.ClearOutlines();
        }

        private sealed class Marker
        {
            public readonly string Id;
            public readonly GameObject GameObject;
            public readonly DebugSphere Sphere;
            public readonly DebugSphere OutlineSphere;
            public StructuralConnectionSnapshot Connection;
            public PartBehavior Parent;
            public PartBehavior Child;
            public Vector3 Center;
            public float BaseRadius;
            public float CurrentRadius;

            public Marker(
                string id,
                GameObject gameObject,
                DebugSphere sphere,
                DebugSphere outlineSphere
            )
            {
                Id = id;
                GameObject = gameObject;
                Sphere = sphere;
                OutlineSphere = outlineSphere;
            }
        }
    }
}