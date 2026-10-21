using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP.Localization;
using Contracts;
using ContractConfigurator.Parameters;

namespace ContractConfigurator
{
    /// <summary>
    /// Static class with extensions to stock classes.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Gets all the parameter's descendents.
        /// </summary>
        /// <param name="p">Contract parameter</param>
        /// <returns>Enumerator of descendents</returns>
        public static IEnumerable<ContractParameter> GetAllDescendents(this IContractParameterHost p)
        {
            for (int i = 0; i < p.ParameterCount; i++)
            {
                ContractParameter child = p.GetParameter(i);
                yield return child;
                foreach (ContractParameter descendent in child.GetAllDescendents())
                {
                    yield return descendent;
                }
            }
        }

        /// <summary>
        /// Gets all the parameter's children.
        /// </summary>
        /// <param name="p">Contract parameter</param>
        /// <returns>Enumerator of descendents</returns>
        public static IEnumerable<ContractParameter> GetChildren(this IContractParameterHost p)
        {
            for (int i = 0; i < p.ParameterCount; i++)
            {
                yield return p.GetParameter(i);
            }
        }

        /// <summary>
        /// Gets all the kerbals for the given roster.
        /// </summary>
        /// <param name="p">Contract parameter</param>
        /// <returns>Enumerator of descendents</returns>
        public static IEnumerable<ProtoCrewMember> AllKerbals(this KerbalRoster roster)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] != null)
                {
                    yield return roster[i];
                }
            }
        }

        /// <summary>
        /// Gets the quantity of the given resource for the vessel.
        /// </summary>
        /// <param name="vessel">Vessel to check</param>
        /// <param name="resource">Resource to check for</param>
        /// <returns></returns>
        public static double ResourceQuantity(this Vessel vessel, PartResourceDefinition resource)
        {
            if (vessel == null)
            {
                return 0.0;
            }

            double quantity = 0.0;
            foreach (Part part in vessel.Parts)
            {
                PartResource pr = part.Resources[resource.name];
                if (pr != null)
                {
                    quantity += pr.amount;
                }
            }
            return quantity;
        }

        /// <summary>
        /// Gets the quantity of the given resource for the vessel.
        /// </summary>
        /// <param name="vessel">Vessel to check</param>
        /// <param name="resource">Resource to check for</param>
        /// <returns></returns>
        public static double ResourceCapacity(this Vessel vessel, PartResourceDefinition resource)
        {
            if (vessel == null)
            {
                return 0.0;
            }

            double quantity = 0.0;
            foreach (Part part in vessel.Parts)
            {
                PartResource pr = part.Resources[resource.name];
                if (pr != null)
                {
                    quantity += pr.maxAmount;
                }
            }
            return quantity;
        }

        /// <summary>
        /// One part of a vessel as GetHashes sees it, read from a loaded Part or from a ProtoPartSnapshot.
        /// </summary>
        private class HashPart
        {
            public uint flightID;
            public int parent = -1;
            public List<int> children = new List<int>();
            public bool decoupler;
            public List<uint> grappledParts = new List<uint>();
        }

        private static bool IsDecouplingModule(string moduleName)
        {
            return moduleName == "ModuleDecouple" || moduleName == "ModuleDockingNode" || moduleName == "ModuleGrappleNode";
        }

        /// <summary>
        /// Create a hash of the vessel.
        /// </summary>
        /// <param name="vessel">The vessel to hash</param>
        /// <returns>A list of hashes for this vessel</returns>
        public static IEnumerable<uint> GetHashes(this Vessel vessel)
        {
            if (vessel.state == Vessel.State.DEAD)
            {
                yield break;
            }

            // A loaded vessel is walked through its live part tree, so parts that were decoupled, destroyed
            // or grabbed since the last save are seen as they are now. An unloaded vessel only has its
            // ProtoPartSnapshots, where parentIdx is the part link that is always there (ProtoPartSnapshot.parent
            // is only set on snapshots made from loaded parts).
            List<HashPart> parts = vessel.loaded && vessel.parts != null && vessel.parts.Count > 0
                ? GetHashParts(vessel.parts)
                : GetHashParts(vessel.protoVessel);
            if (parts == null || parts.Count == 0)
            {
                yield break;
            }

            Dictionary<uint, int> partByFlightID = new Dictionary<uint, int>(parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                HashPart part = parts[i];
                if (part.parent >= 0)
                {
                    parts[part.parent].children.Add(i);
                }
                if (!partByFlightID.ContainsKey(part.flightID))
                {
                    partByFlightID[part.flightID] = i;
                }
            }

            // 0 = not seen, 1 = queued, 2 = processed
            int[] visited = new int[parts.Count];
            Queue<int> queue = new Queue<int>();
            HashSet<uint> dockedParts = new HashSet<uint>();
            Queue<int> otherVessel = new Queue<int>();

            // Add the root
            queue.Enqueue(0);
            visited[0] = 1;

            // Do a BFS of all parts.
            uint hash = 0;
            int count = 0;
            while (queue.Count > 0 || otherVessel.Count > 0)
            {
                // Start a new ship
                if (queue.Count == 0)
                {
                    // Reset our hash
                    if (count != 0)
                    {
                        yield return hash;
                    }
                    count = 0;
                    hash = 0;

                    // Find an unhandled part to use as the new vessel
                    while (otherVessel.Count > 0)
                    {
                        int px = otherVessel.Dequeue();
                        if (visited[px] != 2)
                        {
                            queue.Enqueue(px);
                            break;
                        }
                    }
                    dockedParts.Clear();
                    continue;
                }

                int index = queue.Dequeue();
                HashPart p = parts[index];

                // Check if this is for a new vessel
                if (dockedParts.Contains(p.flightID))
                {
                    otherVessel.Enqueue(index);
                    continue;
                }

                // Just assume all parts can decouple from a decoupler, docking port or grapple, it's
                // easier and effectively the same thing
                if (p.decoupler)
                {
                    // Parent may be missing if this is the root of the stack
                    if (p.parent >= 0)
                    {
                        dockedParts.Add(parts[p.parent].flightID);
                    }

                    // Add all children as possible new vessels
                    foreach (int child in p.children)
                    {
                        dockedParts.Add(parts[child].flightID);
                    }

                    foreach (uint dockedPartUId in p.grappledParts)
                    {
                        if (partByFlightID.TryGetValue(dockedPartUId, out int dockedPart))
                        {
                            otherVessel.Enqueue(dockedPart);
                        }
                    }
                }

                // Go through our child parts
                foreach (int child in p.children)
                {
                    if (visited[child] == 0)
                    {
                        queue.Enqueue(child);
                        visited[child] = 1;
                    }
                }

                // Confirm if parent part has been visited
                if (p.parent >= 0 && visited[p.parent] == 0)
                {
                    queue.Enqueue(p.parent);
                    visited[p.parent] = 1;
                }

                // Add this part to the hash
                if (!p.decoupler)
                {
                    count++;
                    hash ^= p.flightID;
                }

                // We've processed this node
                visited[index] = 2;
            }

            // Return the last hash
            if (count != 0)
            {
                yield return hash;
            }
        }

        private static List<HashPart> GetHashParts(List<Part> vesselParts)
        {
            Dictionary<Part, int> indexOf = new Dictionary<Part, int>(vesselParts.Count);
            for (int i = 0; i < vesselParts.Count; i++)
            {
                indexOf[vesselParts[i]] = i;
            }

            List<HashPart> parts = new List<HashPart>(vesselParts.Count);
            foreach (Part part in vesselParts)
            {
                HashPart hashPart = new HashPart { flightID = part.flightID };
                if (part.parent != null && indexOf.TryGetValue(part.parent, out int parentIndex))
                {
                    hashPart.parent = parentIndex;
                }

                foreach (PartModule pm in part.Modules)
                {
                    if (IsDecouplingModule(pm.moduleName))
                    {
                        hashPart.decoupler = true;
                        if (pm is ModuleGrappleNode grapple && grapple.dockedPartUId != 0)
                        {
                            hashPart.grappledParts.Add(grapple.dockedPartUId);
                        }
                    }
                }
                parts.Add(hashPart);
            }
            return parts;
        }

        private static List<HashPart> GetHashParts(ProtoVessel protoVessel)
        {
            List<ProtoPartSnapshot> snapshots = protoVessel?.protoPartSnapshots;
            if (snapshots == null)
            {
                return null;
            }

            List<HashPart> parts = new List<HashPart>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                ProtoPartSnapshot pps = snapshots[i];
                HashPart hashPart = new HashPart { flightID = pps.flightID };

                // The root's parentIdx points to itself
                if (pps.parentIdx >= 0 && pps.parentIdx < snapshots.Count && pps.parentIdx != i)
                {
                    hashPart.parent = pps.parentIdx;
                }

                foreach (ProtoPartModuleSnapshot pm in pps.modules)
                {
                    if (IsDecouplingModule(pm.moduleName))
                    {
                        hashPart.decoupler = true;
                        if (pm.moduleName == "ModuleGrappleNode")
                        {
                            uint dockedPartUId = 0;
                            if (pm.moduleRef is ModuleGrappleNode grapple)
                            {
                                dockedPartUId = grapple.dockedPartUId;
                            }
                            else if (pm.moduleValues.HasValue("dockUId"))   // Module is unloaded. Try to fetch from ConfigNode directly.
                            {
                                uint.TryParse(pm.moduleValues.GetValue("dockUId"), out dockedPartUId);
                            }

                            if (dockedPartUId != 0)
                            {
                                hashPart.grappledParts.Add(dockedPartUId);
                            }
                        }
                    }
                }
                parts.Add(hashPart);
            }
            return parts;
        }

        /// <summary>
        /// Removes a part from an unloaded vessel. The other parts refer to each other by their index in
        /// protoPartSnapshots (parent, attach nodes, symmetry), and KSP uses those indices to rebuild the
        /// part tree when the vessel loads, so they are moved to keep pointing at the same parts.
        /// </summary>
        /// <param name="protoVessel">The vessel to remove the part from</param>
        /// <param name="pps">The part to remove</param>
        public static void RemovePart(this ProtoVessel protoVessel, ProtoPartSnapshot pps)
        {
            List<ProtoPartSnapshot> snapshots = protoVessel.protoPartSnapshots;
            int removed = snapshots.IndexOf(pps);
            if (removed < 0)
            {
                return;
            }
            int removedParent = pps.parentIdx;
            snapshots.RemoveAt(removed);

            foreach (ProtoPartSnapshot other in snapshots)
            {
                // Anything attached to the removed part now hangs from its parent
                other.parentIdx = ShiftPartIndex(other.parentIdx == removed ? removedParent : other.parentIdx, removed);

                if (other.attachNodes != null)
                {
                    foreach (AttachNodeSnapshot node in other.attachNodes)
                    {
                        node.partIdx = ShiftPartIndex(node.partIdx, removed);
                    }
                }
                if (other.srfAttachNode != null)
                {
                    other.srfAttachNode.partIdx = ShiftPartIndex(other.srfAttachNode.partIdx, removed);
                }
                if (other.symLinkIdxs != null)
                {
                    other.symLinkIdxs.Remove(removed);
                    for (int i = 0; i < other.symLinkIdxs.Count; i++)
                    {
                        other.symLinkIdxs[i] = ShiftPartIndex(other.symLinkIdxs[i], removed);
                    }
                }
                other.symLinks?.Remove(pps);
            }

            if (protoVessel.rootIndex > removed)
            {
                protoVessel.rootIndex--;
            }
        }

        /// <summary>
        /// The new index of a part after the part at index removed was taken out of the list (-1 if it was that part).
        /// </summary>
        private static int ShiftPartIndex(int index, int removed)
        {
            return index == removed ? -1 : index > removed ? index - 1 : index;
        }

        public static string CleanDisplayName(this CelestialBody body, bool lower=false)
        {
            return lower ? Localizer.Format("<<1>>", body.displayName) : Localizer.Format("<<C:1>>", body.displayName);
        }
    }
}
