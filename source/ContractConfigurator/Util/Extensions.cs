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

            // Create the protovessel if it isn't there
            if (vessel.protoVessel == null)
            {
                vessel.protoVessel = new ProtoVessel(vessel);
            }

            if (vessel.protoVessel.protoPartSnapshots == null)
            {
                yield break;
            }

            Queue<ProtoPartSnapshot> queue = new Queue<ProtoPartSnapshot>();
            Dictionary<ProtoPartSnapshot, int> visited = new Dictionary<ProtoPartSnapshot, int>();
            Dictionary<uint, uint> dockedParts = new Dictionary<uint, uint>();
            Queue<ProtoPartSnapshot> otherVessel = new Queue<ProtoPartSnapshot>();

            IEnumerable<ProtoPartSnapshot> parts = vessel.protoVessel.protoPartSnapshots;

            // ProtoPartSnapshot.parent is only set on snapshots made from loaded parts. For a vessel that
            // hasn't been loaded since the save was read it is null on every part, which left only the root
            // part in the hash. Fall back to parentIdx, which is always read from the save.
            List<ProtoPartSnapshot> partList = vessel.protoVessel.protoPartSnapshots;
            Dictionary<ProtoPartSnapshot, ProtoPartSnapshot> parentOf = new Dictionary<ProtoPartSnapshot, ProtoPartSnapshot>();
            for (int i = 0; i < partList.Count; i++)
            {
                ProtoPartSnapshot pps = partList[i];
                ProtoPartSnapshot parent = pps.parent;
                if (parent == null && pps.parentIdx >= 0 && pps.parentIdx < partList.Count && pps.parentIdx != i)
                {
                    parent = partList[pps.parentIdx];
                }
                parentOf[pps] = parent;
            }

            // Add the root
            if (vessel.protoVessel.protoPartSnapshots.Any())
            {
                queue.Enqueue(vessel.protoVessel.protoPartSnapshots.First());
                visited[queue.First()] = 1;
            }

            // Do a BFS of all parts.
            uint hash = 0;
            int count = 0;
            while (queue.Any() || otherVessel.Any())
            {
                bool decoupler = false;

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
                    while (otherVessel.Any())
                    {
                        ProtoPartSnapshot px = otherVessel.Dequeue();
                        if (!visited.TryGetValue(px, out int v) || v != 2)
                        {
                            queue.Enqueue(px);
                            break;
                        }
                    }
                    dockedParts.Clear();
                    continue;
                }

                if (!queue.Any())
                {
                    break;
                }

                ProtoPartSnapshot p = queue.Dequeue();

                // Check if this is for a new vessel
                if (dockedParts.ContainsKey(p.flightID))
                {
                    otherVessel.Enqueue(p);
                    continue;
                }

                // Special handling of certain modules
                foreach (ProtoPartModuleSnapshot pm in p.modules)
                {
                    if (pm.moduleName == "ModuleDecouple" || pm.moduleName == "ModuleDockingNode" || pm.moduleName == "ModuleGrappleNode")
                    {
                        // Just assume all parts can decouple from this, it's easier and
                        // effectively the same thing
                        decoupler = true;

                        // Parent may be null if this is the root of the stack
                        if (parentOf[p] != null)
                        {
                            dockedParts[parentOf[p].flightID] = parentOf[p].flightID;
                        }

                        // Add all children as possible new vessels
                        foreach (ProtoPartSnapshot child in parts.Where(p1 => parentOf[p1] == p))
                        {
                            dockedParts[child.flightID] = child.flightID;
                        }

                        if (pm.moduleName == "ModuleGrappleNode")
                        {
                            uint dockedPartUId = 0;
                            if (pm.moduleRef is ModuleGrappleNode grapple)
                            {
                                dockedPartUId = grapple.dockedPartUId;
                            }
                            else if (pm.moduleValues.HasValue("dockUId"))   // Module is unloaded. Try to fetch from ConfigNode directly.
                            {
                                dockedPartUId = uint.Parse(pm.moduleValues.GetValue("dockUId"));
                            }

                            if (dockedPartUId != 0)
                            {
                                ProtoPartSnapshot dockedPart = parts.Where(p2 => p2.flightID == dockedPartUId).FirstOrDefault();
                                if (dockedPart != null)
                                {
                                    otherVessel.Enqueue(dockedPart);
                                }
                            }
                        }
                    }
                }

                // Go through our child parts
                foreach (ProtoPartSnapshot child in parts.Where(p3 => parentOf[p3] == p))
                {
                    if (!visited.ContainsKey(child))
                    {
                        queue.Enqueue(child);
                        visited[child] = 1;
                    }
                }

                // Confirm if parent part has been visited
                if (parentOf[p] != null && !visited.ContainsKey(parentOf[p]))
                {
                    queue.Enqueue(parentOf[p]);
                    visited[parentOf[p]] = 1;
                }

                // Add this part to the hash
                if (!decoupler)
                {
                    count++;
                    hash ^= p.flightID;
                }

                // We've processed this node
                visited[p] = 2;
            }

            // Return the last hash
            if (count != 0)
            {
                yield return hash;
            }
        }

        public static string CleanDisplayName(this CelestialBody body, bool lower=false)
        {
            return lower ? Localizer.Format("<<1>>", body.displayName) : Localizer.Format("<<C:1>>", body.displayName);
        }
    }
}
