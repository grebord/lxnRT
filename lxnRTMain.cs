//  lxnRT "lxn Resource Transfer" v0.9.1
//  plugin for Kerbal Space Program v1.0.4
//
//  Copyright (c) 2015 Germán Rebord "lxn"
//  All rights reserved.
//
//  Redistribution and use in source and binary forms, with or without
//  modification, are permitted provided that the following conditions
//  are met:
//
//  1.Redistributions of source code must retain the above copyright
//  notice, this list of conditions and the following disclaimer.
//
//  2.Redistributions in binary form must reproduce the above copyright
//  notice, this list of conditions and the following disclaimer in the
//  documentation and/or other materials provided with the distribution.
//
//  THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS "AS IS"
//  AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
//  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
//  PARTICULAR PURPOSE ARE DISCLAIMED.IN NO EVENT SHALL THE AUTHOR OR
//  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
//  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
//  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
//  OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
//  OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace lxnRT
{
    // lxnRT Main Class
    // The plugin will only be active when on a flight scene
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class lxnRTMain : MonoBehaviour
    {
        // Hide or show the plugin's Resource Transfer window
        private bool showgui;
        // Allows or disallows a resource transaction
        private bool transact;
        // Size and initial position of the RT window
        private Rect winCanvas;
        // Key to show or hide the RT window (can be defined by user)
        private string keygui;
        // Maximum distance between vessels for transaction (can be def by user)
        private double maxDist;
        // Set whether the plugin will require vessels to remain below a certain
        // rel speed to make a transaction (can be set by user)
        private double relSpeedLimit;
        // Maximum relative speed between vessels (can be req'd by user)
        private bool reqKerbal;
        // Array of possible resources to transfer (can be modified by user)
        private string[] gResource;
        // Instances a "transaction" to be made (see its class at the bottom) 
        private lxnRTForm crt;
        // Values set with the GUI to setup a transaction
        private int iRes;
        private bool tarIsTar;
        // Vars used to show a transaction is in progress on GUI
        private string l;
        private int iC;

        // Constructor (using Awake instead as advised by Unity)
        public lxnRTMain() { }

        void Awake()
        {
            showgui = false;
            transact = false;
            winCanvas = new Rect(100, 100, 130, 115);
            keygui = "p";
            maxDist = 200.0;
            relSpeedLimit = 0.0;
            reqKerbal = false;
            gResource = new string[16];
            gResource[0] = "LiquidFuel";
            gResource[1] = "Oxidizer";
            gResource[2] = "MonoPropellant";
            gResource[3] = "XenonGas";
            gResource[4] = "SolidFuel";
            gResource[5] = "Ore";
            for (int i = 6; i < 16; i++) gResource[i] = "";
            iRes = 0;
            tarIsTar = false;
            l = "";
            iC = 0;
        }

        // (Init or) Reset some vars after scene change
        void Start()
        {
            loadCfg();
            l = "";
            for (int i = 0; i < 25; i++) l = l + " ";
            iC = 0;
        }

        // Load user-configurable values
        private void loadCfg()
        {
            PluginConfiguration cfgFile = PluginConfiguration.CreateForType<lxnRTMain>();
            cfgFile.load();
            // Set custom keygui
            char tempC = cfgFile.GetValue<char>("keybind", 'p');
            if (char.IsUpper(tempC)) tempC = char.ToLower(tempC);
            if (char.IsLower(tempC) || char.IsDigit(tempC)) keygui = tempC.ToString();
            // Set custom maxDist
            double tempD = cfgFile.GetValue<double>("maxDistance", 200.0);
            if (tempD < 20) tempD = 20;
            if (tempD > 2000) tempD = 2000;
            if (tempD >= 20 && tempD <= 2000) maxDist = tempD;
            // Set relative speed limit requirement
            // This will default to "lax" in future versions
            string tempS = cfgFile.GetValue<string>("relSpeedLimit", "none");
            switch (tempS)
            {
                case "lax":   relSpeedLimit = 16.0; break;
                case "strict": relSpeedLimit = 4.0; break;
                case "none": relSpeedLimit = 0.0; break;
                default: relSpeedLimit = 0.0; break;
            }
            // Set Kerbal presence requirement
            // This will default to True in future versions
            bool tempB = cfgFile.GetValue<bool>("requireKerbal", false);
            reqKerbal = tempB;
            // Set custom gResource[6..15]
            for (int i = 1; i < 11; i++)
            {
                gResource[i + 5] = cfgFile.GetValue<string>("customResource" + i, "");
                // Mayhaps:
                gResource[i + 5].Trim();
                if ((gResource[i + 5] == null) || (gResource[i + 5] == " ")) gResource[i + 5] = "";
            }
        }

        // In charge of carrying on a resource trasaction
        void FixedUpdate()
        {
            if (transact)
            {
                if (checkReq() && checkAITReq())
                {
                    if (crt.doRT()) arRT();
                }
                else arRT();
            }
        }
        // Check additional in-transfer requirements
        private bool checkAITReq()
        {
            if ((getTransID() == crt.getTransID()) && checkRSL())
                return true;
            return false;
        }

        // In charge of setting showgui to display or hide RT window
        void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(keygui)) showgui = !showgui;
        }

        // In charge of drawing the RT window which lets the user configure and start a transaction
        void OnGUI()
        {
            if (showgui) winCanvas = GUI.Window(GUIUtility.GetControlID(FocusType.Passive),
                    winCanvas, lxnRTWindow, "lxnRT v0.9.1");
        }

        private void lxnRTWindow(int winID)
        {
            if (GUI.Button(new Rect(3, 3, 12, 12), "")) showgui = false;
            if (!transact)
            {
                if (checkReq())
                {
                    if (GUI.Button(new Rect(10, 25, 110, 20), quickM()))
                    {
                        tarIsTar = !tarIsTar;
                    }
                    if (GUI.Button(new Rect(10, 58, 16, 14), "<"))
                    {
                        iRes--;
                        if (iRes < 0) iRes++;
                    }
                    if (GUI.Button(new Rect(104, 58, 16, 14), ">"))
                    {
                        iRes++;
                        if (iRes > 15 || gResource[iRes] == "") iRes--;
                    }
                    GUI.Label(new Rect(27, 55, 76, 20), gResource[iRes]);
                    if (!reqKerbal || checkKerbal())
                    {
                        if (checkRSL()) { if (GUI.Button(new Rect(10, 85, 110, 20), "Start")) stRT(); }
                        else GUI.Label(new Rect(15, 80, 105, 20), "Going too fast!");
                    }
                    else GUI.Label(new Rect(10, 80, 110, 20), "No Kerbal present!");
                }
                else
                    GUI.Label(new Rect(15, 25, 110, 80),
                   "Select a target\nvessel.\n\nMax distance:\n" + (int)maxDist + "m."); // 2cast||!2b
            }
            else
            {
                GUI.Label(new Rect(15, 25, 110, 20), "Transferring...");
                GUI.Label(new Rect(17, 52, 110, 20), l);    // Small "animated" indicator
                iC++; if (iC >= 50) iC = 0;                     // to show there's a transaction 
                if (iC % 2 == 0) doSome3d();                // taking place, using _l and _iC
                if (GUI.Button(new Rect(10, 85, 110, 20), "Stop")) arRT();
            }
            GUI.DragWindow();
        }
        private string quickM()
        {
            if (tarIsTar) return "To target";
            else return "From target";
        }
        private void doSome3d()
        {
            if (iC < 17) l = "|" + l;
            else l = " " + l;
            l = l.Remove(l.Length - 1);
        }

        // Check everything's fine before transferring resources (vessel states)
        private bool checkReq()
        {
            if ((TimeWarp.CurrentRateIndex == 0) &&
                (FlightGlobals.fetch.VesselTarget != null) &&
                (FlightGlobals.fetch.VesselTarget.GetType().ToString() == "Vessel") &&
                (getDist() <= maxDist))
                return true;
            return false;
        }

        // Check if there is at least 1 Kerbal in any of the vessels. We don't need
        // additional checks as this executes after checkReq
        private bool checkKerbal()
        {
            if (FlightGlobals.ActiveVessel.GetCrewCount() > 0 ||
                FlightGlobals.fetch.VesselTarget.GetVessel().GetCrewCount() > 0)
                return true;
            return false;
        }

        private bool checkRSL()
        {
            if (relSpeedLimit == 0.0) return true;
            if (FlightGlobals.ship_tgtSpeed <= relSpeedLimit) return true;
            return false;
        }

        // Return the distance between the Active and Targeted vessels (in-game, mts)
        private double getDist()
        {
            return (Vector3d.Distance(FlightGlobals.ActiveVessel.GetWorldPos3D(),
                FlightGlobals.fetch.VesselTarget.GetVessel().GetWorldPos3D()));
        }

        // Return current "transaction ID" used to check for changes in targets while transferring
        private string getTransID()
        {
            return string.Concat(FlightGlobals.fetch.VesselTarget.GetVessel().GetInstanceID().ToString(),
                FlightGlobals.ActiveVessel.GetInstanceID().ToString(), gResource[iRes]);
        }

        // Set and start transaction
        private void stRT()
        {
            crt = new lxnRTForm();
            if (tarIsTar)
            {
                if (crt.setupRT(gResource[iRes], FlightGlobals.ActiveVessel.Parts,
                    FlightGlobals.fetch.VesselTarget.GetVessel().Parts, getTransID()))
                    transact = true;
                else arRT();
            }
            else
            {
                if (crt.setupRT(gResource[iRes], FlightGlobals.fetch.VesselTarget.GetVessel().Parts,
                    FlightGlobals.ActiveVessel.Parts, getTransID()))
                    transact = true;
                else arRT();
            }
        }

        // Un-set and stop transaction
        private void arRT()
        {
            transact = false;
            crt = null;
        }

        // Make sure everything has stopped before scene transition
        void OnDestroy()
        {
            arRT();
            showgui = false;
        }
    }

    // This is a "Transaction Form" which stores the transaction settings and
    // handles interaction with the vessels during resource transfer
    class lxnRTForm
    {
        // Stored "transaction ID"
        private string transID;
        // Resource to transfer in a transaction
        private string ucResource;
        // Source and Target parts from intervening vessels
        private Part sourcePart;
        private Part targetPart;
        // Rate of transfer in each physics update
        private double vRate;

        public lxnRTForm()
        {
            transID = "";
            ucResource = "";
            sourcePart = null;
            targetPart = null;
            vRate = -1;
        }

        public string getTransID() { return transID; }

        // Method to configure a transaction. Returns false if something isn't right
        public bool setupRT(string leResource, List<Part> sourceVesselPartList, List<Part> targetVesselPartList, string newTransID)
        {
            transID = newTransID;
            ucResource = leResource;
            sourcePart = getPart(sourceVesselPartList);
            targetPart = getPart(targetVesselPartList);
            vRate = getRate(getVesselCapacity(sourceVesselPartList), getVesselCapacity(targetVesselPartList));
            if (sourcePart == null || targetPart == null || vRate <= 0) return false;
            return true;
        }

        // Return a part that contains a specific resource - or null
        private Part getPart(List<Part> myVesselPartList)
        {
            foreach (Part myPart in myVesselPartList)
                foreach (PartResource myPartResource in myPart.Resources)
                    if (myPartResource.resourceName == ucResource)
                        return myPart;
            return null;
        }

        // Return a vessel's total capacity of a specific resource
        private double getVesselCapacity(List<Part> myVesselPartList)
        {
            double totAmount = 0;
            foreach (Part myPart in myVesselPartList)
                foreach (PartResource myPartResource in myPart.Resources)
                    if (myPartResource.resourceName == ucResource)
                        totAmount = totAmount + myPartResource.maxAmount;
            return totAmount;
        }

        // Select the lowest total resource capicity between the vessels to 
        // define a rate of transfer. There are many reasons why I chose
        // this simple method, but if you try new ones don't forget to let
        // me know how they do!
        private double getRate(double totAmount1, double totAmount2)
        {
            return Math.Min(totAmount1, totAmount2) / 300.0;
        }

        // Method in charge of actually (finally!) transferring resources
        // between the vessels. Returns true if it can't transfer anymore
        public bool doRT()
        {
            double sflow = sourcePart.RequestResource(ucResource, vRate,
                ResourceFlowMode.STAGE_PRIORITY_FLOW);
            double tflow = targetPart.RequestResource(ucResource, -sflow,
                ResourceFlowMode.STAGE_PRIORITY_FLOW);
            if (sflow > -tflow)
                sourcePart.RequestResource(ucResource, -(sflow + tflow),
                    ResourceFlowMode.STAGE_PRIORITY_FLOW);
            if (tflow == 0) return true;
            return false;
        }
    }
}
