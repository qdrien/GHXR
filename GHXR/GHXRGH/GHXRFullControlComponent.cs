using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// Careful that we need the whole output of the bin/ folder loaded in GH.
// The _GrasshopperDeveloperSettings Rhino command can be used to do so.

namespace GHXR
{
    public class GHXRFullControlComponent : GH_Component
    {
        private MqttClient client;

        private List<string> logs = new List<string>();
        private string lastMeshData = "[]";
        private string lastLocalisedMeshData = "[]";
        private string lastParameterData = "[]";
        private string lastGeometryPositionData = "[]";
        private string parameterControlChannel = "ghxr/parameters/control";
        private string geometryPositionsControlChannel = "ghxr/geometry/positions/control";

        Dictionary<string, IGH_Param> parameters = new Dictionary<string, IGH_Param>();

        private Queue<string> parameterControlMsgQueue = new Queue<string>();
        private Queue<string> geometryPositionsControlMsgQueue = new Queue<string>();

        private bool alreadyWarned;

        public GHXRFullControlComponent()
          : base("GHXR Full control", "GHXR Full control",
              "Connects Grasshopper to GHXR modules. Sends meshes and parameters; " +
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
            pManager.AddTextParameter("Meshes channel", "Meshes channel",
                "MQTT channel to be used to publish meshes updates.", GH_ParamAccess.item);
            pManager.AddTextParameter("Parameters sharing channel", "P. sharing channel",
                "MQTT channel to be used to publish available parameters.", GH_ParamAccess.item);
            pManager.AddTextParameter("Parameters control channel", "P. control channel",
                "MQTT channel to subscribe to so as to receive value updates.", GH_ParamAccess.item);
            pManager.AddTextParameter("Geometry positions sharing channel", "G. sharing channel",
                "MQTT channel to be used to publish geometry positions updates.", GH_ParamAccess.item);
            pManager.AddTextParameter("Geometry positions control channel", "G. control channel",
                "MQTT channel to subscribe to so as to receive value updates.", GH_ParamAccess.item);

            pManager.AddMeshParameter("Meshes", "Meshes", "Meshes to be shared.", GH_ParamAccess.list);
            pManager[6].Optional = true;
            pManager.AddTextParameter("Geometry positions data", "G. pos. data", "Position data for shared geometries.", GH_ParamAccess.item);
            pManager[7].Optional = true; //TODO: shouldnt it be hidden from the GH interface? or simply kept as field (/!\persistence)?
            pManager.AddGenericParameter("Parameters", "Parameters",
                "Parameter(s) to be shared.", GH_ParamAccess.list);
            pManager[8].Optional = true;
            pManager.AddGenericParameter("Localised Meshes", "Localised Meshes", "Meshes to be shared, including GPS position", GH_ParamAccess.list);
            pManager[9].Optional = true;
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

            if (!alreadyWarned)
                if (System.Windows.Forms.MessageBox.Show("Be aware that the GHXRFullControlComponent is an early/test version, it may contain bugs.\n" +
                    "You should probably use GHXRSimplifiedComponent instead, even though it provides less configuration options.",
                    "Warning about GHXRFullControlComponent", MessageBoxButtons.OK) == DialogResult.OK)
                    alreadyWarned = true;

            #region Get input parameters

            string brokerAdress = "localhost";
            string meshesChannel = "ghxr/geometry/meshes";
            string parameterSharingChannel = "ghxr/parameters/share";
            string geometryPositionsSharingChannel = "ghxr/geometry/positions/share";


            if (!DA.GetData(0, ref brokerAdress)) return;
            if (!DA.GetData(1, ref meshesChannel)) return;
            if (!DA.GetData(2, ref parameterSharingChannel)) return;
            if (!DA.GetData(3, ref parameterControlChannel)) return;
            if (!DA.GetData(4, ref geometryPositionsSharingChannel)) return;
            if (!DA.GetData(5, ref geometryPositionsControlChannel)) return;

            List<Mesh> meshes = new List<Mesh>();
            List<ShareableParameter> shareableParameters = new List<ShareableParameter>();
            parameters = new Dictionary<string, IGH_Param>();

            if (DA.GetDataList(6, meshes))
            {
                Log(meshes.Count + " meshes to share.");
            }

            string geometryPositionsJson = "[]";
            DA.GetData(7, ref geometryPositionsJson);


            IList<IGH_Param> parameterInputs = Params.Input[8].Sources;

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

            List<GH_ObjectWrapper> objectWrappers = new List<GH_ObjectWrapper>();

            DA.GetDataList(9, objectWrappers);
            List<LocalisedMesh> localisedMeshes = new List<LocalisedMesh>();

            for (int i = 0; i < objectWrappers.Count; i++)
            {
                GH_ObjectWrapper objectWrapper = objectWrappers[i];
                LocalisedMesh localisedMesh = objectWrapper.Value as LocalisedMesh;
                if (localisedMesh != null)
                {
                    localisedMeshes.Add(localisedMesh);
                    //AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"test parameter: {localisedMesh.Mesh}/{localisedMesh.Latitude}/{localisedMesh.Longitude}/{localisedMesh.Heading}");
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Parameter {i} connected to the LocalisedMesh input is not a localised mesh.");
                }
            }

            //AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Got {localisedMeshes.Count} meshes.");



            //IList<IGH_Param> localisedMeshInputs = Params.Input[9].Sources;
            /*foreach (IGH_Param localisedMeshInput in localisedMeshInputs)
            {
                LocalisedMesh localisedMesh = localisedMeshInput as LocalisedMesh;
                if (localisedMesh == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Parameter {localisedMeshInput.NickName} is not a localised mesh.");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "" + localisedMeshInput.Type);
                    return;
                }

            }*/


            #endregion

            #region Establish/verify MQTT connection (uses MQTT 3.1.1)

            if (client == null)
            {
                Log("client was null, instantiating a new one. Broker: " + brokerAdress);
                client = new MqttClient(brokerAdress);
                client.MqttMsgPublishReceived += MessageReceived;
                client.Subscribe(new string[] { parameterControlChannel, geometryPositionsControlChannel }
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
                    "ghxr/status/" + clientId,
                    "offline",
                    false, //clean session
                    60);
                Log("connectionCode: " + connectionCode);
                client.Publish("ghxr/status/" + clientId, System.Text.Encoding.UTF8.GetBytes("online"), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
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
                //no need to send the new parameters to subscribers here, 
                //since ExpireSolution() will trigger a new call to SolveInstance that will do so.
                return;
            }

            #endregion

            #region Process geometrypositioncontrol queue (return afterwards if not empty)

            //making sure the queue cannot be updated from another thread while we process it here.
            lock (geometryPositionsControlMsgQueue)
            {
                requireExpireSolution = geometryPositionsControlMsgQueue.Count > 0;
                Log("About to process a geometrypositioncontrol queue of size: " + geometryPositionsControlMsgQueue.Count);
                while (geometryPositionsControlMsgQueue.Count > 0)
                {
                    string message = geometryPositionsControlMsgQueue.Dequeue();
                    Log("Dequeuing and processing: " + message);

                    try
                    {
                        List<PositionData> newPositions = JsonConvert.DeserializeObject<List<PositionData>>(message);
                        Log("Got " + newPositions.Count + " positions:");
                        foreach (PositionData position in newPositions)
                        {
                            Log(position.lat + " ; " + position.lon + " (heading=" + position.hdg + ")");
                        }

                        //should probably make some sanitary check here
                        //(otherwise and as of now, none of the items from the queue are meaningful, except the last one)

                        Log("updating geometry position data ");
                        GH_Panel geometryPositionsPanel = Params.Input[6].Sources[0] as GH_Panel;
                        geometryPositionsPanel.UserText = message;
                    }
                    catch
                    {
                        Log("Could not deserialize this geometry positions json, not processing it.");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not deserialize this geometry positions json, not processing it.");
                    }
                }
            }

            if (requireExpireSolution)
            {
                Log("Updates were processed, expiring solution and stopping here.");
                Instances.DocumentEditor.Invoke((MethodInvoker)delegate { ExpireSolution(true); });
                //no need to send the new parameters to subscribers here, 
                //since ExpireSolution() will trigger a new call to SolveInstance that will do so.
                return;
            }

            #endregion


            Log("parametercontrol and geometrypositioncontrol queues were empty, processing mesh(es) and parameter(s).");

            #region Convert mesh to json (+send if needed)

            List<ShareableMesh> shareableMeshes = new List<ShareableMesh>();

            foreach (Mesh mesh in meshes)
            {
                shareableMeshes.Add(new ShareableMesh(mesh));
            }

            string meshData = JsonConvert.SerializeObject(shareableMeshes);

            Log($"will process {localisedMeshes.Count} localised meshes.");


            List<ShareableLocalisedMesh> shareableLocalisedMeshes = new List<ShareableLocalisedMesh>();

            foreach (LocalisedMesh mesh in localisedMeshes)
            {
                shareableLocalisedMeshes.Add(new ShareableLocalisedMesh(mesh));
            }

            string localisedMeshData = JsonConvert.SerializeObject(shareableLocalisedMeshes);
            Log("Serialised json: " + localisedMeshData);
            if (localisedMeshData != lastLocalisedMeshData)
            {
                Log("Mesh change detected, publishing the new one.");
                client.Publish(meshesChannel, System.Text.Encoding.UTF8.GetBytes(meshData), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
                lastLocalisedMeshData = localisedMeshData;
                Log(localisedMeshData);
            }

            #endregion

            #region send geometry positions (if updated)

            List<PositionData> geometryPositions = new List<PositionData>();
            if (!geometryPositionsJson.Equals(lastGeometryPositionData))
            {
                try
                {
                    geometryPositions = JsonConvert.DeserializeObject<List<PositionData>>(geometryPositionsJson);
                    Log(geometryPositions.Count + " geometry positions to share.");
                    if (meshes.Count != geometryPositions.Count)
                    {
                        Log("The number of meshes differs from the number of positions.");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The number of meshes differs from the number of positions.");
                    }
                    Log("geometrypositions input change detected, publishing the new data.");
                    client.Publish(geometryPositionsSharingChannel, System.Text.Encoding.UTF8.GetBytes(geometryPositionsJson), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
                    lastGeometryPositionData = geometryPositionsJson;
                    Log(geometryPositionsJson);
                }
                catch
                {
                    Log("Could not deserialize geometrypositions json, not sending it.");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not deserialize geometrypositions json, not sending it.");
                }
            }

            #endregion

            #region convert input parameters to json (+send if needed)
            string parameterData = JsonConvert.SerializeObject(shareableParameters);

            if (parameterData != lastParameterData)
            {
                Log("parameter input change detected, publishing the new data.");
                client.Publish(parameterSharingChannel, System.Text.Encoding.UTF8.GetBytes(parameterData), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
                lastParameterData = parameterData;
                Log(parameterData);
            }

            #endregion

            DA.SetDataList(0, logs);
        }

        private void MessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string topic = e.Topic;
            string message = System.Text.Encoding.UTF8.GetString(e.Message);

            Log("Rcv[" + topic + "]:" + message);

            if (topic == parameterControlChannel)
            {
                //making sure the queue cannot be edited by another thread (e.g. the main/UI one) while we enqueue our message
                lock (parameterControlMsgQueue)
                {
                    parameterControlMsgQueue.Enqueue(message);
                }
            }
            else if (topic == geometryPositionsControlChannel)
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

        private void Log(string v)
        {
            RhinoApp.WriteLine(GetType() + ":" + v);
            logs.Add(v);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (!alreadyWarned)
                if (System.Windows.Forms.MessageBox.Show("Be aware that the GHXRFullControlComponent is an early/test version, it may contain bugs.\n" +
                    "You should probably use GHXRSimplifiedComponent instead, even though it provides less configuration options.",
                    "Warning about GHXRFullControlComponent", MessageBoxButtons.OK) == DialogResult.OK)
                    alreadyWarned = true;
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
                return Resources.logo_red; //Made with PhotoFiltre 7, using pictograms (grasshopper, goggles) from FreePik
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("aeb612b4-a314-44a6-8c55-ff7a3471457b"); }
        }
    }
}
