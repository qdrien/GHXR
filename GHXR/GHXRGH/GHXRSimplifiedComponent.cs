using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Newtonsoft.Json;
using Grasshopper;
using System.Windows.Forms;
using Grasshopper.Kernel.Types;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

// Careful that we need the whole output of the bin/ folder loaded in GH.
// The _GrasshopperDeveloperSettings Rhino command can be used to do so.

namespace GHXR
{
    public class GHXRSimplifiedComponent : GH_Component
    {
        private const string geometryPositionsControlSubTopic = "/geometry/positions";
        private const string parameterControlSubTopic = "/parameters/control";
        private const string statusSubTopic = "/status";
        private const string geometryMeshesSubTopic = "/geometry/meshes";
        private const string parameterShareSubTopic = "/parameters/share";
        private MqttClient client;

        private List<string> logs = new List<string>();
        private string baseTopic = "";
        private string lastMeshData = "[]";
        private string lastParameterData = "[]";

        Dictionary<string, IGH_Param> parameters = new Dictionary<string, IGH_Param>();

        private Queue<string> parameterControlMsgQueue = new Queue<string>();
        private Queue<string> geometryPositionsControlMsgQueue = new Queue<string>();
        //private float updateDelay = .5f;
        private Stopwatch stopWatch = new Stopwatch();

        private static int meshUpdateDelayMs = 400;
        private static int parameterUpdateDelayMs = 300;
        private DelayedMethodCaller meshDelayedUpdateCaller = new DelayedMethodCaller(meshUpdateDelayMs);
        private DelayedMethodCaller parameterDelayedUpdateCaller = new DelayedMethodCaller(parameterUpdateDelayMs);

        public GHXRSimplifiedComponent()
          : base("GHXR Simplified", "GHXR Simplified",
              "Connects Grasshopper to GHXR modules using default parameters. Sends meshes and parameters; " +
                "also receives value updates.",
              "GHXR", "GHXR")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("MQTT Broker", "Broker",
                "URL of the MQTT Broker to be used.", GH_ParamAccess.item);
            pManager.AddTextParameter("Base MQTT topic", "Base topic", 
                "Base topic to be used (should be specific to your project, will publish messages in subtopics.).", GH_ParamAccess.item);

            pManager.AddGenericParameter("Meshes", "Meshes", "Localised mesh(es) to be shared (include GPS position, use the LocalisedMesh component).", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddGenericParameter("Parameters", "Parameters",
                "Parameter(s) to be shared.", GH_ParamAccess.list);
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Logs", "Logs", "Logs", GH_ParamAccess.list);
            //TODO: may want to use multiple logging outputs 
            //(e.g. write out last known value for each channel)
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Log("SolveInstance called.");

            #region Get input parameters
            //0=broker 1=base topic 2=meshes 3=params

            string brokerAddress = "";

            //required parameters
            if (!DA.GetData(0, ref brokerAddress)) return;
            if (!DA.GetData(1, ref baseTopic)) return;

            DA.SetDataList(0, logs);

            baseTopic = baseTopic.TrimEnd(new[] { '/'});

            //optional parameters
            List<GH_ObjectWrapper> objectWrappers = new List<GH_ObjectWrapper>();

            DA.GetDataList(2, objectWrappers);
            List<LocalisedMesh> localisedMeshes = new List<LocalisedMesh>();

            for (int i = 0; i < objectWrappers.Count; i++)
            {
                GH_ObjectWrapper objectWrapper = objectWrappers[i];
                LocalisedMesh localisedMesh = objectWrapper.Value as LocalisedMesh;
                if (localisedMesh != null)
                {
                    localisedMeshes.Add(localisedMesh);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Parameter {i} connected to the LocalisedMesh input is not a localised mesh.");
                }
            }

            Log($"Meshes: {localisedMeshes.Count}/{objectWrappers.Count} ok.");

            DA.SetDataList(0, logs);

            List<ShareableParameter> shareableParameters = new List<ShareableParameter>();
            parameters = new Dictionary<string, IGH_Param>();

            IList<IGH_Param> parameterInputs = Params.Input[3].Sources;

            foreach (IGH_Param parameterInput in parameterInputs)
            {
                switch (parameterInput.ComponentGuid.ToString())
                {
                    case "2e78987b-9dfb-42a2-8b76-3923ac8bd91a": //toggle
                        GH_BooleanToggle toggle = parameterInput as GH_BooleanToggle;
                        parameters.Add(parameterInput.InstanceGuid.ToString(), toggle);
                        shareableParameters.Add(new ShareableParameter.ShareableToggle
                        {
                            Type = "toggle",
                            Guid = parameterInput.InstanceGuid.ToString(),
                            Name = parameterInput.Name,
                            NickName = parameterInput.NickName,
                            Value = toggle.Value
                        });
                        break;

                    case "57da07bd-ecab-415d-9d86-af36d7073abc": //slider
                        GH_NumberSlider slider = parameterInput as GH_NumberSlider;
                        parameters.Add(parameterInput.InstanceGuid.ToString(), slider);
                        shareableParameters.Add(new ShareableParameter.ShareableSlider
                        {
                            Type = "slider",
                            Guid = parameterInput.InstanceGuid.ToString(),
                            Name = parameterInput.Name,
                            NickName = parameterInput.NickName,
                            Value = (float)slider.CurrentValue,
                            Accuracy = (int)slider.Slider.Type,
                            Min = (float)slider.Slider.Minimum,
                            Max = (float)slider.Slider.Maximum,
                            Epsilon = (float)slider.Slider.Epsilon,
                            DecimalPlaces = slider.Slider.DecimalPlaces
                        });
                        break;

                    case "00027467-0d24-4fa7-b178-8dc0ac5f42ec": //list
                        GH_ValueList list = parameterInput as GH_ValueList;
                        parameters.Add(parameterInput.InstanceGuid.ToString(), list);
                        shareableParameters.Add(new ShareableParameter.ShareableList
                        {
                            Type = "list",
                            Guid = parameterInput.InstanceGuid.ToString(),
                            Name = parameterInput.Name,
                            NickName = parameterInput.NickName,
                            ListMode = (int)list.ListMode,
                            //could be made more efficient by using standard for loops (without lambdas/linq)
                            Values = list.ListItems.ConvertAll(item => new ShareableParameter.ShareableListItem()
                            {
                                Expression = item.Expression,
                                Selected = item.Selected,
                                Name = item.Name
                            })
                        });
                        break;

                    case "bcac2747-348b-4edd-ae1f-77a782cebbdd": //knob
                        GH_DialKnob knob = parameterInput as GH_DialKnob;
                        parameters.Add(parameterInput.InstanceGuid.ToString(), knob);
                        shareableParameters.Add(new ShareableParameter.ShareableKnob
                        {
                            Type = "knob",
                            Guid = parameterInput.InstanceGuid.ToString(),
                            Name = parameterInput.Name,
                            NickName = parameterInput.NickName,
                            Value = (float)knob.Value,
                            Decimals = knob.Decimals,
                            Range = (float)knob.Range,
                            LimitKnobValue = knob.Limit,
                            Min = (float)knob.Minimum,
                            Max = (float)knob.Maximum
                        });
                        break;

                    /*case "339c0ee1-cf11-444f-8e10-65c9150ea755": //colour picker
                        GH_ColourPickerObject colour = parameterInput as GH_ColourPickerObject;
                        parameters.Add(parameterInput.InstanceGuid.ToString(), colour);
                        shareableParameters.Add(new ShareableParameter.ShareableColour
                        {
                            Type = "colour",
                            Guid = parameterInput.InstanceGuid.ToString(),
                            Name = parameterInput.Name,
                            NickName = parameterInput.NickName,
                            Value = colour.Colour //sends RGB not ARGB, need alpha as well (maybe split in 4 integer values)
                        }); 
                        break;*/

                    default:
                        Log(parameterInput.Type + " not supported (parameter name: " + parameterInput.NickName + " / guid: " + parameterInput.ComponentGuid + ").");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, parameterInput.Type + " not supported (parameter name: " + parameterInput.NickName + " / guid: " + parameterInput.ComponentGuid + ").");
                        break;
                }
            }

            #endregion

            #region Establish/verify MQTT connection (uses MQTT 3.1.1)

            if (client == null)
            {
                Log("client was null, instantiating a new one. Broker: " + brokerAddress);
                client = new MqttClient(brokerAddress);
                client.MqttMsgPublishReceived += MessageReceived;
                client.Subscribe(new string[] { baseTopic + parameterControlSubTopic, baseTopic + geometryPositionsControlSubTopic }
                , new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            }

            if (client.IsConnected)
            {
                Log("client already connected.");
            }
            else
            {
                Log("client wasn't connected, connecting...");
                string clientId = "GHPlugin";
                byte connectionCode = client.Connect(clientId,
                    null, null, //user/pw
                    true, //retain
                    MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                    true, //lastwill
                    baseTopic + statusSubTopic + "/" + clientId,
                    "offline",
                    false, //clean session
                    60);
                Log("connectionCode: " + connectionCode);
                client.Publish(baseTopic + statusSubTopic + "/" + clientId, System.Text.Encoding.UTF8.GetBytes("online"), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
            }

            #endregion

            DA.SetDataList(0, logs);

            #region Process parametercontrol queue (return afterwards if not empty)

            bool requireExpireSolution;

            //making sure the queue cannot be updated from another thread while we process it here.
            lock (parameterControlMsgQueue)
            {
                requireExpireSolution = parameterControlMsgQueue.Count > 0;
                Log("About to process a parametercontrol queue of size: " + parameterControlMsgQueue.Count);
                while (parameterControlMsgQueue.Count > 0)
                {
                    string message = parameterControlMsgQueue.Dequeue();
                    Log("Dequeuing and processing: " + message);
                    try
                    {
                        List<ShareableParameter> parametersToBeChanged = JsonConvert.DeserializeObject<List<ShareableParameter>>
                            (message, new ShareableParameterConverter());

                        Log("Processing a parameter control json.");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Processing a parameter control json.");

                        foreach (ShareableParameter parameterToBeChanged in parametersToBeChanged)
                        {
                            switch (parameterToBeChanged.Type)
                            {
                                case "toggle":
                                    ShareableParameter.ShareableToggle modifiedToggle =
                                        parameterToBeChanged as ShareableParameter.ShareableToggle;
                                    GH_BooleanToggle actualToggle = parameters[parameterToBeChanged.Guid] as GH_BooleanToggle;
                                    Log("Modifying toggle value of " + parameterToBeChanged.Guid + " to " + modifiedToggle.Value);
                                    actualToggle.Value = modifiedToggle.Value;
                                    break;
                                case "slider":
                                    ShareableParameter.ShareableSlider modifiedSlider =
                                        parameterToBeChanged as ShareableParameter.ShareableSlider;
                                    GH_NumberSlider actualSlider = parameters[parameterToBeChanged.Guid] as GH_NumberSlider;
                                    Log("Modifying slider value of " + parameterToBeChanged.Guid + " to " + modifiedSlider.Value);
                                    actualSlider.SetSliderValue((decimal)modifiedSlider.Value);
                                    break;
                                case "list":
                                    ShareableParameter.ShareableList modifiedList =
                                        parameterToBeChanged as ShareableParameter.ShareableList;
                                    GH_ValueList actualList = parameters[parameterToBeChanged.Guid] as GH_ValueList;
                                    Log("Modifying valueList; guid=" + parameterToBeChanged.Guid);

                                    if (actualList.ListMode == GH_ValueListMode.CheckList)
                                    {
                                        Log("this is a checklist.");
                                        for (int i = 0; i < actualList.ListItems.Count; i++)
                                        {
                                            GH_ValueListItem item = actualList.ListItems[i];
                                            ShareableParameter.ShareableListItem modifiedItem = modifiedList.Values[i];
                                            item.Selected = modifiedItem.Selected;
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 0; i < modifiedList.Values.Count; i++)
                                        {
                                            ShareableParameter.ShareableListItem modifiedItem = modifiedList.Values[i];
                                            if (modifiedItem.Selected)
                                            {
                                                if (!actualList.ListItems[i].Selected)
                                                {
                                                    Log("Will change selection to item " + i);
                                                    actualList.SelectItem(i);
                                                }

                                                break;
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    Log("unrecognised parameter type: " + parameterToBeChanged.Type
                                        + " (for parameter " + parameterToBeChanged.Guid + ")");
                                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "unrecognised parameter type: " + parameterToBeChanged.Type
                                        + " (for parameter " + parameterToBeChanged.Guid + ")");
                                    break;
                            }
                        }
                    }
                    catch
                    {
                        Log("Could not deserialize this parameter control json, not processing it.");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not deserialize this parameter control json, not processing it.");
                    }
                }
            }

            if (requireExpireSolution)
            {
                Log("Updates were processed, expiring solution and stopping here.");
                Instances.DocumentEditor.Invoke((MethodInvoker)delegate { ExpireSolution(true); });
                //TODO: can we simply expire affected components?
                //TODO: if not, should try GH_Document.NewSolution(false);
                //TODO: or GH.RunSolver(False); //(VB? find equivalent?)
                //TODO: or GH_Document.ScheduleSolution(1, callbackDelegate); //executes the callback 1ms afterwards, there we can expire only the objects we changed, and this will trigger a new solveinstance
                //no need to send the new parameters to subscribers here, 
                //since ExpireSolution() will trigger a new call to SolveInstance that will do so.
                return;
            }

            #endregion

            Log("parametercontrol and geometrypositioncontrol queues were empty, processing mesh(es) and parameter(s).");

            #region Convert mesh to json (+send if needed)

            Log($"will process {localisedMeshes.Count} localised meshes.");


            List<ShareableLocalisedMesh> shareableLocalisedMeshes = new List<ShareableLocalisedMesh>();

            for (int i = 0; i < localisedMeshes.Count; i++)
            {
                LocalisedMesh mesh = localisedMeshes[i];
                shareableLocalisedMeshes.Add(new ShareableLocalisedMesh(mesh));
                if (i < 5)
                    Log($"Mesh with: gps={mesh.Latitude},{mesh.Longitude} and hdg={mesh.Heading}");
            }

            string meshData = JsonConvert.SerializeObject(shareableLocalisedMeshes);
            //Log("Serialised json: " + meshData);
            if (meshData != lastMeshData)
            {
                Log($"Mesh change detected, will publish in {meshUpdateDelayMs}ms.");
                lastMeshData = meshData;
                meshDelayedUpdateCaller.CallMethod(() =>
                {
                    client.Publish(baseTopic + geometryMeshesSubTopic, System.Text.Encoding.UTF8.GetBytes(lastMeshData), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
                    Log("Actually publishing the last mesh data");
                });

                //WriteToFile(baseTopic + geometryMeshesSubTopic + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"), meshData);
            }

            #endregion

            #region convert input parameters to json (+send if needed)
            string parameterData = JsonConvert.SerializeObject(shareableParameters);

            if (parameterData != lastParameterData)
            {
                Log($"Parameter change detected, will publish in {parameterUpdateDelayMs}ms.");
                lastParameterData = parameterData;
                parameterDelayedUpdateCaller.CallMethod(() =>
                {
                    client.Publish(baseTopic + parameterShareSubTopic, System.Text.Encoding.UTF8.GetBytes(lastParameterData), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
                    Log("Actually publishing the last parameter data");
                });


                //WriteToFile(baseTopic + parameterShareSubTopic + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"), parameterData);
            }

            #endregion

            DA.SetDataList(0, logs);
        }

        private void MessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string topic = e.Topic;
            string message = System.Text.Encoding.UTF8.GetString(e.Message);

            Log("Rcv[" + topic + "]:" + message);

            if (topic == baseTopic + parameterControlSubTopic)
            {
                //making sure the queue cannot be edited by another thread (e.g. the main/UI one) while we enqueue our message
                lock (parameterControlMsgQueue)
                {
                    parameterControlMsgQueue.Enqueue(message);
                }
            }
            else if (topic == baseTopic + geometryPositionsControlSubTopic)
            {
                //making sure the queue cannot be edited by another thread (e.g. the main/UI one) while we enqueue our message
                lock (geometryPositionsControlMsgQueue)
                {
                    geometryPositionsControlMsgQueue.Enqueue(message);
                }
            }
            else
            {
                Log("Unexpected topic.");
                return;
            }


            //need to trigger a call to solveinstance since changes to parameters have to be made on the main/UI thread
            Instances.DocumentEditor.Invoke((MethodInvoker)delegate { ExpireSolution(true); });
        }

        private void WriteToFile(string fileName, string json)
        {
            string folder = @"D:\dev\GHXRTable\GHXRTableGH\data dumps\";
            fileName = fileName.Replace('/', '_');
            fileName = fileName.Replace('\\', '_');

            // Fullpath. You can direct hardcode it if you like.  
            string fullPath = folder + fileName + ".json";
            RhinoApp.WriteLine("Will dump to file: " + fullPath);

            // Write array of strings to a file using WriteAllLines.  
            // If the file does not exists, it will create a new file.  
            // This method automatically opens the file, writes to it, and closes file  
            File.WriteAllText(fullPath, json);
        }

        private void Log(string v)
        {
            RhinoApp.WriteLine(GetType() + ":" + v);
            logs.Add(v);
        }


        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                //return null;
                return Resources.logo; //Made with PhotoFiltre 7, using pictograms (grasshopper, goggles) from FreePik
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("eac4971b-9b9d-4742-a152-4b256420b042"); }
        }
    }
}