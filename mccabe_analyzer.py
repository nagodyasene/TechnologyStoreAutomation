#!/usr/bin/env python3
"""
McCabe Cyclomatic Complexity Analyzer for C# Projects
Analyzes all methods in C# files and calculates their McCabe complexity score.
"""

import os
import re
import sys
from pathlib import Path
from dataclasses import dataclass
from typing import List, Tuple
import json

@dataclass
class MethodInfo:
    """Represents a method with its complexity information."""
    file_path: str
    class_name: str
    method_name: str
    complexity: int
    start_line: int
    
def find_cs_files(root_dir: str, exclude_patterns: List[str] = None) -> List[str]:
    """Find all C# files in the directory, excluding obj/bin folders."""
    if exclude_patterns is None:
        exclude_patterns = ['obj', 'bin', '.git']
    
    cs_files = []
    for root, dirs, files in os.walk(root_dir):
        # Filter out excluded directories
        dirs[:] = [d for d in dirs if d not in exclude_patterns]
        
        for file in files:
            if file.endswith('.cs'):
                cs_files.append(os.path.join(root, file))
    
    return cs_files

def extract_methods(content: str, file_path: str) -> List[Tuple[str, str, str, int]]:
    """
    Extract methods from C# content.
    Returns list of (class_name, method_name, method_body, start_line).
    """
    methods = []
    lines = content.split('\n')
    
    # Track current class
    current_class = "Unknown"
    class_stack = []
    
    # Regex patterns
    class_pattern = re.compile(r'(?:public|private|protected|internal|sealed|abstract|static|partial)?\s*(?:class|struct|record)\s+(\w+)')
    method_pattern = re.compile(r'(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed)?\s*(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed)?\s*(?:[\w<>\[\],\s]+)\s+(\w+)\s*\([^)]*\)\s*(?:where\s+\w+\s*:\s*\w+)?\s*(?:\{|$)', re.MULTILINE)
    
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        
        # Skip comments and empty lines
        if stripped.startswith('//') or stripped.startswith('/*') or not stripped:
            i += 1
            continue
        
        # Check for class definition
        class_match = class_pattern.search(line)
        if class_match and '{' in line[class_match.end():]:
            current_class = class_match.group(1)
        elif class_match:
            # Class definition might span multiple lines
            current_class = class_match.group(1)
        
        # Check for method definition
        # Look for patterns like: public void MethodName(...) {
        method_match = re.search(r'(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed|\s)+\s*([\w<>\[\],\?]+)\s+(\w+)\s*\([^)]*\)\s*(?:\{|$)', line)
        
        if method_match and not any(kw in line for kw in ['class ', 'interface ', 'struct ', 'enum ', 'namespace ']):
            method_name = method_match.group(2)
            
            # Skip constructors, properties getters/setters
            if method_name in ['get', 'set', 'add', 'remove', 'value']:
                i += 1
                continue
                
            # Find method body
            start_line = i + 1
            brace_count = 0
            method_body_lines = []
            found_opening_brace = False
            
            j = i
            while j < len(lines):
                current_line = lines[j]
                
                for char in current_line:
                    if char == '{':
                        brace_count += 1
                        found_opening_brace = True
                    elif char == '}':
                        brace_count -= 1
                
                method_body_lines.append(current_line)
                
                if found_opening_brace and brace_count == 0:
                    break
                    
                j += 1
            
            if method_body_lines:
                method_body = '\n'.join(method_body_lines)
                methods.append((current_class, method_name, method_body, start_line))
                i = j
        
        i += 1
    
    return methods

def calculate_mccabe_complexity(method_body: str) -> int:
    """
    Calculate McCabe Cyclomatic Complexity for a method.
    
    Formula: CC = 1 + number of decision points
    
    Decision points include:
    - if, else if, elif
    - for, foreach, while, do
    - case (in switch)
    - catch
    - && (and operator)
    - || (or operator)
    - ?: (ternary operator)
    - ?? (null coalescing operator)
    """
    complexity = 1  # Base complexity
    
    # Remove string literals to avoid false positives
    # Replace string literals with empty strings
    method_body = re.sub(r'"[^"\\]*(?:\\.[^"\\]*)*"', '""', method_body)
    method_body = re.sub(r"'[^'\\]*(?:\\.[^'\\]*)*'", "''", method_body)
    
    # Remove single-line comments
    method_body = re.sub(r'//.*$', '', method_body, flags=re.MULTILINE)
    
    # Remove multi-line comments
    method_body = re.sub(r'/\*.*?\*/', '', method_body, flags=re.DOTALL)
    
    # Count decision points
    decision_patterns = [
        (r'\bif\s*\(', 'if'),
        (r'\belse\s+if\s*\(', 'else if'),
        (r'\bfor\s*\(', 'for'),
        (r'\bforeach\s*\(', 'foreach'),
        (r'\bwhile\s*\(', 'while'),
        (r'\bdo\s*\{', 'do'),
        (r'\bcase\s+[^:]+:', 'case'),
        (r'\bcatch\s*\(', 'catch'),
        (r'\bcatch\s*\{', 'catch'),  # catch without exception type
        (r'&&', '&&'),
        (r'\|\|', '||'),
        (r'\?[^?]', '?:'),  # Ternary operator (not ??)
        (r'\?\?', '??'),  # Null coalescing
    ]
    
    for pattern, name in decision_patterns:
        matches = re.findall(pattern, method_body)
        complexity += len(matches)
    
    # Subtract 1 for else if since 'if' in 'else if' is already counted
    else_if_count = len(re.findall(r'\belse\s+if\s*\(', method_body))
    complexity -= else_if_count
    
    return max(1, complexity)

def analyze_file(file_path: str) -> List[MethodInfo]:
    """Analyze a single C# file and return method complexity information."""
    try:
        with open(file_path, 'r', encoding='utf-8-sig') as f:
            content = f.read()
    except Exception as e:
        print(f"Error reading {file_path}: {e}", file=sys.stderr)
        return []
    
    methods = extract_methods(content, file_path)
    results = []
    
    for class_name, method_name, method_body, start_line in methods:
        complexity = calculate_mccabe_complexity(method_body)
        results.append(MethodInfo(
            file_path=file_path,
            class_name=class_name,
            method_name=method_name,
            complexity=complexity,
            start_line=start_line
        ))
    
    return results

def get_complexity_rating(complexity: int) -> str:
    """Get a human-readable rating for the complexity score."""
    if complexity <= 5:
        return "✅ Basit (Simple)"
    elif complexity <= 10:
        return "🟡 Orta (Moderate)"
    elif complexity <= 20:
        return "🟠 Karmaşık (Complex)"
    else:
        return "🔴 Çok Karmaşık (Very Complex)"

def main():
    """Main entry point."""
    # Get the project root directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    root_dir = script_dir
    
    print("=" * 80)
    print("McCabe Karmaşıklık Ölçütü Analizi (McCabe Cyclomatic Complexity Analysis)")
    print("=" * 80)
    print(f"\nProje dizini: {root_dir}\n")
    
    # Find all C# files
    cs_files = find_cs_files(root_dir)
    print(f"Bulunan C# dosya sayısı: {len(cs_files)}\n")
    
    # Analyze all files
    all_methods: List[MethodInfo] = []
    
    for file_path in cs_files:
        methods = analyze_file(file_path)
        all_methods.extend(methods)
    
    if not all_methods:
        print("Hiç metot bulunamadı!")
        return
    
    # Sort by complexity (descending)
    all_methods.sort(key=lambda m: m.complexity, reverse=True)
    
    # Print results
    print(f"Toplam analiz edilen metot sayısı: {len(all_methods)}\n")
    print("-" * 80)
    
    # Group by file for organized output
    files_dict = {}
    for method in all_methods:
        rel_path = os.path.relpath(method.file_path, root_dir)
        if rel_path not in files_dict:
            files_dict[rel_path] = []
        files_dict[rel_path].append(method)
    
    # Print detailed results
    print("\n📊 DETAYLI SONUÇLAR (Karmaşıklığa göre sıralı)")
    print("=" * 80)
    
    for method in all_methods:
        rel_path = os.path.relpath(method.file_path, root_dir)
        rating = get_complexity_rating(method.complexity)
        print(f"\n📁 {rel_path}")
        print(f"   📌 Sınıf: {method.class_name}")
        print(f"   📌 Metot: {method.method_name}() [Satır: {method.start_line}]")
        print(f"   📌 Karmaşıklık: {method.complexity} - {rating}")
    
    # Summary statistics
    print("\n" + "=" * 80)
    print("📈 ÖZET İSTATİSTİKLER (Summary Statistics)")
    print("=" * 80)
    
    complexities = [m.complexity for m in all_methods]
    avg_complexity = sum(complexities) / len(complexities)
    max_complexity = max(complexities)
    min_complexity = min(complexities)
    
    # Count by category
    simple = sum(1 for c in complexities if c <= 5)
    moderate = sum(1 for c in complexities if 5 < c <= 10)
    complex_count = sum(1 for c in complexities if 10 < c <= 20)
    very_complex = sum(1 for c in complexities if c > 20)
    
    print(f"\n📊 Toplam Metot Sayısı: {len(all_methods)}")
    print(f"📊 Ortalama Karmaşıklık: {avg_complexity:.2f}")
    print(f"📊 Minimum Karmaşıklık: {min_complexity}")
    print(f"📊 Maksimum Karmaşıklık: {max_complexity}")
    
    print(f"\n📊 Kategori Dağılımı:")
    print(f"   ✅ Basit (1-5): {simple} metot ({100*simple/len(all_methods):.1f}%)")
    print(f"   🟡 Orta (6-10): {moderate} metot ({100*moderate/len(all_methods):.1f}%)")
    print(f"   🟠 Karmaşık (11-20): {complex_count} metot ({100*complex_count/len(all_methods):.1f}%)")
    print(f"   🔴 Çok Karmaşık (21+): {very_complex} metot ({100*very_complex/len(all_methods):.1f}%)")
    
    # List most complex methods
    print("\n" + "=" * 80)
    print("⚠️ EN KARMAŞIK 10 METOT (Top 10 Most Complex Methods)")
    print("=" * 80)
    
    for i, method in enumerate(all_methods[:10], 1):
        rel_path = os.path.relpath(method.file_path, root_dir)
        rating = get_complexity_rating(method.complexity)
        print(f"\n{i}. {method.class_name}.{method.method_name}()")
        print(f"   Dosya: {rel_path}:{method.start_line}")
        print(f"   Karmaşıklık: {method.complexity} - {rating}")
    
    # Export to JSON
    output_file = os.path.join(root_dir, "mccabe_results.json")
    results_json = {
        "summary": {
            "total_methods": len(all_methods),
            "average_complexity": round(avg_complexity, 2),
            "min_complexity": min_complexity,
            "max_complexity": max_complexity,
            "simple_count": simple,
            "moderate_count": moderate,
            "complex_count": complex_count,
            "very_complex_count": very_complex
        },
        "methods": [
            {
                "file": os.path.relpath(m.file_path, root_dir),
                "class": m.class_name,
                "method": m.method_name,
                "complexity": m.complexity,
                "line": m.start_line,
                "rating": get_complexity_rating(m.complexity)
            }
            for m in all_methods
        ]
    }
    
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(results_json, f, ensure_ascii=False, indent=2)
    
    print(f"\n💾 Sonuçlar JSON dosyasına kaydedildi: {output_file}")
    print("\n" + "=" * 80)

if __name__ == "__main__":
    main()
