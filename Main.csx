#r "libs/dnlib.dll"
#r "libs/de4dot.blocks.dll"

using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

var CheckFunctionSig = "bIlMrpQbF2jKcKKRgCG.Eli3lqQCaYkYjXOatl8::iHBHQquze1IgS()";

private static bool CheckCall(Instr instr, string methodFullName)
{
	if (instr.OpCode.Code != Code.Call && instr.OpCode.Code != Code.Callvirt)
		return false;
	var calledMethod = instr.Operand as IMethod;
	if (calledMethod == null)
		return false;
	return calledMethod.FullName == methodFullName;
}


var args = Environment.GetCommandLineArgs();
var module = ModuleDefMD.Load(args[2]);
var checkFunctionSig = (args.Length >= 4 ? args[3] :null) ?? CheckFunctionSig;

foreach (var t in module.GetTypes())
{
	for (var i = 0; i < t.CustomAttributes.Count; i++)
	{
		var cattr = t.CustomAttributes[i];
		if (cattr.TypeFullName != "System.ComponentModel.LicenseProviderAttribute")
			continue;
		t.CustomAttributes.RemoveAt(i);
		i--;
	}

	foreach (var method in t.Methods.Where(m => m.Name == ".cctor" && m.HasBody))
	{
		var blocks = new Blocks(method);
		foreach (var block in blocks.MethodBlocks.GetAllBlocks())
		{
			var instrs = block.Instructions;
			if (instrs.Count >= 3)
			{
				var firsti = instrs[0];
				var secondi = instrs[1];
				var thirdi = instrs[2];
				if (firsti.OpCode.Code == Code.Ldtoken && firsti.Operand is ITypeDefOrRef &&
					CheckCall(secondi, "System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)") &&
					CheckCall(thirdi, "System.Void System.ComponentModel.LicenseManager::Validate(System.Type)"))
					block.Remove(0, 3);
			}
		}

		IList<Instruction> allInstructions;
		IList<ExceptionHandler> allExceptionHandlers;
		blocks.GetCode(out allInstructions, out allExceptionHandlers);
		DotNetUtils.RestoreBody(blocks.Method, allInstructions, allExceptionHandlers);
	}

	foreach (var method in t.Methods.Where(m => (m.Name == ".ctor" || m.Name == ".cctor") && m.HasBody))
	{
		var blocks = new Blocks(method);
		foreach (var block in blocks.MethodBlocks.GetAllBlocks())
		{
			var instrs = block.Instructions;
			if (instrs.Count >= 1 && CheckCall(instrs[0], $"System.Void {checkFunctionSig}"))
				block.Remove(0, 1);
		}

		IList<Instruction> allInstructions;
		IList<ExceptionHandler> allExceptionHandlers;
		blocks.GetCode(out allInstructions, out allExceptionHandlers);
		DotNetUtils.RestoreBody(blocks.Method, allInstructions, allExceptionHandlers);
	}
}
// The name of output must be compatible with its strong name
module.Write(@"SmartQuant.dll");
