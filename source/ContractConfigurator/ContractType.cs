﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Agents;
using ContractConfigurator.ExpressionParser;

namespace ContractConfigurator
{
    public class ContractRequirementException : Exception
    {
        public ContractRequirementException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Class for capturing all contract type details.
    /// </summary>
    public class ContractType : IContractConfiguratorFactory
    {
        private static Dictionary<string, ContractType> contractTypes = new Dictionary<string,ContractType>();
        public static IEnumerable<ContractType> AllContractTypes { get { return contractTypes.Values; } }
        public static IEnumerable<ContractType> AllValidContractTypes
        {
            get
            {
                return contractTypes.Values.Where(ct => ct.enabled);
            }
        }
        public static IEnumerable<string> AllValidContractTypeNames
        {
            get
            {
                return AllValidContractTypes.Select<ContractType, string>(ct => ct.name);
            }
        }
        
        public static ContractType GetContractType(string name)
        {
            if (contractTypes.ContainsKey(name))
            {
                return contractTypes[name];
            }
            return null;
        }

        public static void ClearContractTypes()
        {
            contractTypes.Clear();
        }

        protected List<ParameterFactory> paramFactories = new List<ParameterFactory>();
        protected List<BehaviourFactory> behaviourFactories = new List<BehaviourFactory>();
        protected List<ContractRequirement> requirements = new List<ContractRequirement>();

        public IEnumerable<ParameterFactory> ParamFactories { get { return paramFactories; } }
        public IEnumerable<BehaviourFactory> BehaviourFactories { get { return behaviourFactories; } }
        public IEnumerable<ContractRequirement> Requirements { get { return requirements; } }

        public bool expandInDebug = false;
        public bool hasWarnings { get; set; }
        public bool enabled { get; private set; }
        public string config { get; private set; }
        public int hash { get; private set; }
        public string log { get; private set; }
        public DataNode dataNode { get; private set; }

        // Contract attributes
        public string name;
        public ContractGroup group;
        public string title;
        public string tag;
        public string notes;
        public string description;
        public string topic;
        public string subject;
        public string motivation;
        public string synopsis;
        public string completedMessage;
        public Agent agent;
        public float minExpiry;
        public float maxExpiry;
        public float deadline;
        public bool cancellable;
        public bool declinable;
        public bool autoAccept;
        public List<Contract.ContractPrestige> prestige;
        public CelestialBody targetBody;
        protected List<CelestialBody> targetBodies;
        protected Vessel targetVessel;
        protected List<Vessel> targetVessels;
        protected Kerbal targetKerbal;
        protected List<Kerbal> targetKerbals;
        public int maxCompletions;
        public int maxSimultaneous;
        public float rewardScience;
        public float rewardReputation;
        public float rewardFunds;
        public float failureReputation;
        public float failureFunds;
        public float advanceFunds;
        public double weight;
        public bool trace = false;

        private Dictionary<string, bool> dataValues = new Dictionary<string, bool>();
        public Dictionary<string, bool> uniqueValues = new Dictionary<string, bool>();

        public ContractType(string name)
        {
            this.name = name;
            contractTypes.Add(name, this);

            // Member defaults
            group = null;
            agent = null;
            minExpiry = 0;
            maxExpiry = 0;
            deadline = 0;
            cancellable = true;
            declinable = true;
            autoAccept = false;
            prestige = new List<Contract.ContractPrestige>();
            maxCompletions = 0;
            maxSimultaneous = 0;
            rewardScience = 0.0f;
            rewardReputation = 0.0f;
            rewardFunds = 0.0f;
            failureReputation = 0.0f;
            failureFunds = 0.0f;
            advanceFunds = 0.0f;
            weight = 1.0;
            enabled = true;
        }

        /// <summary>
        /// Loads the contract type details from the given config node.
        /// </summary>
        /// <param name="configNode">The config node to load from.</param>
        /// <returns>Whether the load was successful.</returns>
        public bool Load(ConfigNode configNode)
        {
            LoggingUtil.LogLevel origLogLevel = LoggingUtil.logLevel;

            try
            {
                // Logging on
                LoggingUtil.CaptureLog = true;

                dataNode = new DataNode(configNode.GetValue("name"), this);

                ConfigNodeUtil.SetCurrentDataNode(dataNode);
                bool valid = true;

                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "name", x => name = x, this);

                // Try to turn on trace mode
                valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "trace", x => trace = x, this, false);
                if (trace)
                {
                    LoggingUtil.logLevel = LoggingUtil.LogLevel.VERBOSE;
                    LoggingUtil.LogWarning(this, "Tracing enabled for contract type " + name);
                }

                // Load contract text details
                valid &= ConfigNodeUtil.ParseValue<ContractGroup>(configNode, "group", x => group = x, this, (ContractGroup)null);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "title", x => title = x, this);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "tag", x => tag = x, this, "");
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "description", x => description = x, this, (string)null);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "topic", x => topic = x, this, (string)null);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "subject", x => subject = x, this, (string)null);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "motivation", x => motivation = x, this, (string)null);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "notes", x => notes = x, this, (string)null);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "synopsis", x => synopsis = x, this);
                valid &= ConfigNodeUtil.ParseValue<string>(configNode, "completedMessage", x => completedMessage = x, this);

                // Load optional attributes
                valid &= ConfigNodeUtil.ParseValue<Agent>(configNode, "agent", x => agent = x, this, (Agent)null);
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "minExpiry", x => minExpiry = x, this, 1.0f, x => Validation.GE(x, 0.0f));
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "maxExpiry", x => maxExpiry = x, this, 7.0f, x => Validation.GE(x, minExpiry));
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "deadline", x => deadline = x, this, 0.0f, x => Validation.GE(x, 0.0f));
                valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "cancellable", x => cancellable = x, this, true);
                valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "declinable", x => declinable = x, this, true);
                valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "autoAccept", x => autoAccept = x, this, false);
                valid &= ConfigNodeUtil.ParseValue<List<Contract.ContractPrestige>>(configNode, "prestige", x => prestige = x, this, new List<Contract.ContractPrestige>());
                valid &= ConfigNodeUtil.ParseValue<CelestialBody>(configNode, "targetBody", x => targetBody = x, this, (CelestialBody)null);
            
                valid &= ConfigNodeUtil.ParseValue<int>(configNode, "maxCompletions", x => maxCompletions = x, this, 0, x => Validation.GE(x, 0));
                valid &= ConfigNodeUtil.ParseValue<int>(configNode, "maxSimultaneous", x => maxSimultaneous = x, this, 0, x => Validation.GE(x, 0));

                // Load rewards
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "rewardFunds", x => rewardFunds = x, this, 0.0f, x => Validation.GE(x, 0.0f));
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "rewardReputation", x => rewardReputation = x, this, 0.0f, x => Validation.GE(x, 0.0f));
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "rewardScience", x => rewardScience = x, this, 0.0f, x => Validation.GE(x, 0.0f));
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "failureFunds", x => failureFunds = x, this, 0.0f, x => Validation.GE(x, 0.0f));
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "failureReputation", x => failureReputation = x, this, 0.0f, x => Validation.GE(x, 0.0f));
                valid &= ConfigNodeUtil.ParseValue<float>(configNode, "advanceFunds", x => advanceFunds = x, this, 0.0f, x => Validation.GE(x, 0.0f));

                // Load other values
                valid &= ConfigNodeUtil.ParseValue<double>(configNode, "weight", x => weight = x, this, 1.0, x => Validation.GE(x, 0.0f));

                // Load DATA nodes
                foreach (ConfigNode data in ConfigNodeUtil.GetChildNodes(configNode, "DATA"))
                {
                    Type type = null;
                    bool requiredValue = true;
                    bool uniqueValue = false;
                    bool activeUniqueValue = false;
                    valid &= ConfigNodeUtil.ParseValue<Type>(data, "type", x => type = x, this);
                    valid &= ConfigNodeUtil.ParseValue<bool>(data, "requiredValue", x => requiredValue = x, this, true);
                    valid &= ConfigNodeUtil.ParseValue<bool>(data, "uniqueValue", x => uniqueValue = x, this, false);
                    valid &= ConfigNodeUtil.ParseValue<bool>(data, "activeUniqueValue", x => activeUniqueValue = x, this, false);

                    if (type != null)
                    {
                        foreach (ConfigNode.Value pair in data.values)
                        {
                            string name = pair.name;
                            if (name != "type" && name != "requiredValue" && name != "uniqueValue" && name != "activeUniqueValue")
                            {
                                object value = null;

                                // Create the setter function
                                Type actionType = typeof(Action<>).MakeGenericType(type);
                                Delegate del = Delegate.CreateDelegate(actionType, value, typeof(ContractType).GetMethod("NullAction"));

                                // Get the ParseValue method
                                MethodInfo method = typeof(ConfigNodeUtil).GetMethods(BindingFlags.Static | BindingFlags.Public).
                                    Where(m => m.Name == "ParseValue" && m.GetParameters().Count() == 4).Single();
                                method = method.MakeGenericMethod(new Type[] { type });

                                // Invoke the ParseValue method
                                valid &= (bool)method.Invoke(null, new object[] { data, name, del, this });

                                dataValues[name] = requiredValue;

                                if (uniqueValue || activeUniqueValue)
                                {
                                    uniqueValues[name] = activeUniqueValue;
                                }
                            }
                        }
                    }
                }

                // Check for unexpected values - always do this last
                valid &= ConfigNodeUtil.ValidateUnexpectedValues(configNode, this);

                log = LoggingUtil.capturedLog;
                LoggingUtil.CaptureLog = false;

                // Load parameters
                foreach (ConfigNode contractParameter in ConfigNodeUtil.GetChildNodes(configNode, "PARAMETER"))
                {
                    ParameterFactory paramFactory = null;
                    valid &= ParameterFactory.GenerateParameterFactory(contractParameter, this, out paramFactory);
                    if (paramFactory != null)
                    {
                        paramFactories.Add(paramFactory);
                        if (paramFactory.hasWarnings)
                        {
                            hasWarnings = true;
                        }
                    }
                }

                // Load behaviours
                foreach (ConfigNode requirementNode in ConfigNodeUtil.GetChildNodes(configNode, "BEHAVIOUR"))
                {
                    BehaviourFactory behaviourFactory = null;
                    valid &= BehaviourFactory.GenerateBehaviourFactory(requirementNode, this, out behaviourFactory);
                    if (behaviourFactory != null)
                    {
                        behaviourFactories.Add(behaviourFactory);
                        if (behaviourFactory.hasWarnings)
                        {
                            hasWarnings = true;
                        }
                    }
                }

                // Load requirements
                foreach (ConfigNode requirementNode in ConfigNodeUtil.GetChildNodes(configNode, "REQUIREMENT"))
                {
                    ContractRequirement requirement = null;
                    valid &= ContractRequirement.GenerateRequirement(requirementNode, this, out requirement);
                    if (requirement != null)
                    {
                        requirements.Add(requirement);
                        if (requirement.hasWarnings)
                        {
                            hasWarnings = true;
                        }
                    }
                }

                // Logging on
                LoggingUtil.CaptureLog = true;

                // Check we have at least one valid parameter
                if (paramFactories.Count() == 0)
                {
                    LoggingUtil.LogError(this.GetType(), ErrorPrefix() + ": Need at least one parameter for a contract!");
                    valid = false;
                }

                // Do the deferred loads
                valid &= ConfigNodeUtil.ExecuteDeferredLoads();

                config = configNode.ToString();
                hash = config.GetHashCode();
                enabled = valid;
                log += LoggingUtil.capturedLog;
                LoggingUtil.CaptureLog = false;

                return valid;
            }
            catch
            {
                enabled = false;
                throw;
            }
            finally
            {
                LoggingUtil.logLevel = origLogLevel;
            }
        }

        /// <summary>
        /// Generates and loads all the parameters required for the given contract.
        /// </summary>
        /// <param name="contract"></param>
        /// <returns>Whether the generation was successful.</returns>
        public bool GenerateBehaviours(ConfiguredContract contract)
        {
            return BehaviourFactory.GenerateBehaviours(contract, behaviourFactories);
        }

        /// <summary>
        /// Generates and loads all the parameters required for the given contract.
        /// </summary>
        /// <param name="contract">Contract to load parameters for</param>
        /// <returns>Whether the generation was successful.</returns>
        public bool GenerateParameters(ConfiguredContract contract)
        {
            return ParameterFactory.GenerateParameters(contract, contract, paramFactories);
        }

        /// <summary>
        /// Tests whether a contract can be offered.
        /// </summary>
        /// <param name="contract">The contract</param>
        /// <returns>Whether the contract can be offered.</returns>
        public bool MeetRequirements(ConfiguredContract contract)
        {
            try
            {
                // Hash check
                if (contract.ContractState == Contract.State.Offered && contract.hash != hash)
                {
                    throw new ContractRequirementException("Contract definition changed.");
                }

                // Check prestige
                if (prestige.Count > 0 && !prestige.Contains(contract.Prestige))
                {
                    throw new ContractRequirementException("Wrong prestige level.");
                }

                // Checks for maxSimultaneous/maxCompletions
                if (maxSimultaneous != 0 || maxCompletions != 0)
                {
                    // Get the count of active contracts - excluding ours
                    int activeContracts = ContractSystem.Instance.GetCurrentContracts<ConfiguredContract>().
                        Count(c => c.contractType != null && c.contractType.name == name);
                    if (contract.ContractState == Contract.State.Offered || contract.ContractState == Contract.State.Active)
                    {
                        activeContracts--;
                    }

                    // Check if we're breaching the active limit
                    if (maxSimultaneous != 0 && activeContracts >= maxSimultaneous)
                    {
                        throw new ContractRequirementException("Too many active contracts.");
                    }

                    // Check if we're breaching the completed limit
                    if (maxCompletions != 0)
                    {
                        int finishedContracts = ContractSystem.Instance.GetCompletedContracts<ConfiguredContract>().
                            Count(c => c.contractType != null && c.contractType.name == name);
                        if (finishedContracts + activeContracts >= maxCompletions)
                        {
                            throw new ContractRequirementException("Too many completed/active/offered contracts.");
                        }
                    }
                }

                // Check the group values
                if (group != null)
                {
                    CheckContractGroup(contract, group);
                }

                // Check special values are not null
                if (contract.contractType == null)
                {
                    foreach (KeyValuePair<string, bool> pair in dataValues)
                    {
                        // Only check if it is a required value
                        if (pair.Value)
                        {
                            string name = pair.Key;

                            object o = dataNode[name];
                            if (o == null)
                            {
                                throw new ContractRequirementException("'" + name + "' was null.");
                            }
                            else if (o == typeof(List<>))
                            {
                                PropertyInfo prop = o.GetType().GetProperty("Count");
                                int count = (int)prop.GetValue(o, null);
                                if (count == 0)
                                {
                                    throw new ContractRequirementException("'" + name + "' had zero count.");
                                }
                            }
                            else if (o == typeof(Vessel))
                            {
                                Vessel v = (Vessel)o;

                                if (v.state == Vessel.State.DEAD)
                                {
                                    throw new ContractRequirementException("Vessel '" + v.vesselName + "' is dead.");
                                }
                            }
                        }
                    }
                }

                // Check for unique values against other contracts of the same type
                foreach (KeyValuePair<string, bool> pair in uniqueValues.Where(p => contract.uniqueData.ContainsKey(p.Key)))
                {
                    string key = pair.Key;
                    bool checkActiveOnly = pair.Value;

                    foreach (ConfiguredContract otherContract in ContractSystem.Instance.GetCurrentContracts<ConfiguredContract>().
                        Where(c => c.contractType != null && c.contractType.name == name && c != contract && c.uniqueData.ContainsKey(key) &&
                            (c.ContractState == Contract.State.Active || c.ContractState == Contract.State.Offered || !checkActiveOnly)))
                    {
                        if (contract.uniqueData[key].Equals(otherContract.uniqueData[key]))
                        {
                            throw new ContractRequirementException("Failed on unique value check for key '" + key + "'.");
                        }
                    }
                }

                // Check the captured requirements
                if (!ContractRequirement.RequirementsMet(contract, this, requirements))
                {
                    throw new ContractRequirementException("Failed on contract requirement check.");
                }

                return true;
            }
            catch (ContractRequirementException e)
            {
                LoggingUtil.LogLevel level = contract.ContractState == Contract.State.Offered ? LoggingUtil.LogLevel.DEBUG : LoggingUtil.LogLevel.VERBOSE;
                string prefix = contract.contractType != null ? "Cancelling contract of type " + name + " (" + contract.Title + "): " :
                    "Didn't generate contract type " + name + ": ";
                LoggingUtil.Log(level, this.GetType(), prefix + e.Message);
                return false;
            }
        }

        protected bool CheckContractGroup(ConfiguredContract contract, ContractGroup group)
        {
            if (group != null)
            {
                // Check the group active limit
                int activeContracts = ContractSystem.Instance.GetCurrentContracts<ConfiguredContract>().Count(c => c.contractType != null && group.BelongsToGroup(c.contractType));
                if (contract.ContractState == Contract.State.Offered || contract.ContractState == Contract.State.Active)
                {
                    activeContracts--;
                }

                if (group.maxSimultaneous != 0 && activeContracts >= group.maxSimultaneous)
                {
                    throw new ContractRequirementException("Too many active contracts in group (" + group.name + ").");
                }

                // Check the group completed limit
                if (group.maxCompletions != 0)
                {
                    int finishedContracts = ContractSystem.Instance.GetCompletedContracts<ConfiguredContract>().Count(c => c.contractType != null && group.BelongsToGroup(c.contractType));
                    if (finishedContracts + activeContracts >= maxCompletions)
                    {
                        throw new ContractRequirementException("Too many completed contracts in group (" + group.name + ").");
                    }
                }

                return CheckContractGroup(contract, group.parent);
            }

            return true;
        }

        public static void NullAction(object o)
        {
        }

        /// <summary>
        /// Gets the identifier for the contract type.
        /// </summary>
        /// <returns>String for the contract type.</returns>
        public override string ToString()
        {
            return "CONTRACT_TYPE [" + name + "]";
        }
        
        public string ErrorPrefix()
        {
            return "CONTRACT_TYPE '" + name + "'";
        }

        public string ErrorPrefix(ConfigNode configNode)
        {
            return ErrorPrefix();
        }
    }
}
