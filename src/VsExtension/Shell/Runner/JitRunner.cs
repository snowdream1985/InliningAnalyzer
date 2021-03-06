﻿using InliningAnalyzer;
using InliningAnalyzer.Graph.Dependencies;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InliningAnalyzer.Collector;

namespace VsExtension.Shell.Runner
{
    public class JitRunner
    {
        private readonly string _assemblyFile;
        private readonly JitTarget _jitTarget;
        private readonly TargetScope _targetScope;
        private readonly ILogger _outputLogger;
        private readonly IJitHostPathResolver _pathResolver;
        private readonly bool _recordEventDetails;

        public AssemblyCallGraph UnorderedCallGraph { get; set; }

        public JitRunner(string assemblyFile, JitTarget jitTarget, TargetScope targetScope, ILogger outputLogger, IJitHostPathResolver pathResolver, bool recordEventDetails = false)
        {
            _assemblyFile = assemblyFile;
            _jitTarget = jitTarget;
            _targetScope = targetScope;
            _outputLogger = outputLogger;
            _pathResolver = pathResolver;
            _recordEventDetails = recordEventDetails;
        }

        public AssemblyCallGraph Run()
        {
            var callGraph = RunJitCompiler(CreateUnorderedController());
            if (_recordEventDetails)
                UnorderedCallGraph = callGraph;

            if (_targetScope != null && _targetScope.ScopeType == ScopeType.Method)
                return callGraph;

            var dependencyGraph = DependencyGraphBuilder.BuildFromCallGraph(callGraph);
            DependencyResolver resolver = new DependencyResolver(dependencyGraph);
            var methodList = resolver.GetOrderedMethodList();

            if (methodList.Methods.Count == 0)
                return callGraph;

            string methodListFile = SerializeMethodList(methodList);
            try
            {
                var orderedCallGraph = RunJitCompiler(CreateOrderedController(methodListFile));
                return orderedCallGraph;
            }
            finally
            {
                if (File.Exists(methodListFile))
                    File.Delete(methodListFile);
            }
        }

        public string SerializeMethodList(MethodCompilationList methodList)
        {
            string tempfile = Path.GetTempFileName();

            using (var file = File.OpenWrite(tempfile))
            {
                Serializer.Serialize(file, methodList);
            }
            return tempfile;
        }

        private JitHostController CreateUnorderedController()
        {
            return new JitHostController(_assemblyFile, _jitTarget, _targetScope, null, _pathResolver);
        }

        private JitHostController CreateOrderedController(string methodListFile)
        {
            return new JitHostController(_assemblyFile, _jitTarget, _targetScope, methodListFile, _pathResolver);
        }

        private AssemblyCallGraph RunJitCompiler(JitHostController jitController)
        {
            using (var etwCollector = new EtwCollector(_recordEventDetails))
            {
                jitController.StartProcess();
                jitController.Process.OutputDataReceived += JitHostOutputDataReceived;
                jitController.Process.ErrorDataReceived += JitHostOutputDataReceived;
                jitController.Process.BeginOutputReadLine();
                jitController.Process.BeginErrorReadLine();

                etwCollector.StartEventTrace(jitController.Process.Id);
                jitController.RunJitCompilation();
                etwCollector.StopEventTrace();

                jitController.Process.OutputDataReceived -= JitHostOutputDataReceived;
                jitController.Process.ErrorDataReceived -= JitHostOutputDataReceived;

                CallGraphPostProcessor.Process(etwCollector.AssemblyCallGraph);
                return etwCollector.AssemblyCallGraph;
            }
        }

        private void JitHostOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _outputLogger.WriteText(e.Data);
        }
    }
}
